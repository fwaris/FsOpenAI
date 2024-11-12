namespace FsOpenAI.GenAI
open System

type AnswerWithCitations = 
    {
        CitationIds : string list
        Answer : string
    }

type Citation =
    {
        Id: string
        Title: string
        Text: string
    }

module StreamParser = 
    type State = 
        {
            SourceChunk : string
            Index : int
        }
        with 
            member this.Step = { this with Index = this.Index + 1 }
            member this.Current = if this.Index >= this.SourceChunk.Length then None else Some this.SourceChunk.[this.Index] 
            member this.NextChunk s = { this with SourceChunk = s; Index = 0 }
            static member Empty = {SourceChunk = ""; Index = 0 }

    type Parser<'a> = 
        State -> 
            State *             //new state 
            Output<'a> *        //see below
            string option       //optional string value that can be streamed out
    and Output<'a> = 
        | Empty of Parser<'a>                   //parser is at end of current chunk - get next chunk and coninue with the returned parser
        | Done of 'a option                     //parser has completed with an optional value
        | Fail of string                        //failed to parse

    let inline append acc o = match o with Some s -> s::acc | _ -> acc

    let bind a = fun (s:State)-> s, Done (Some a), None

    let rec map f (p:Parser<'a>) (s:State) =
        match p s with 
        | s, Done v, o    -> s, Done (f v), o
        | s, Fail msg,  o -> s, Fail msg,          o
        | s, Empty p,   o -> s, Empty (map f p),   o

    ///Combine two parsers in sequence
    let rec combine (p1:Parser<'a>) (f: 'a option -> string option -> Parser<'b>) s = 
        match p1 s with
        | s, Done v, o     -> (f v o) s
        | s, Fail msg, o   -> s, Fail msg, o
        | s, Empty p1, o   -> s, Empty (combine p1 f), o
    
    let (++) (s:string option) (o:string option) = match s, o   with Some s, Some o -> Some (s + o) | None, Some p | Some p, None -> Some p | _ -> None

    //carry along any intermediate stream output till its bubbled up to the top for release
    //let carryOutput p v o = match o with None -> p | o -> fun s -> match p s with s, r, o' -> s, r, o ++ o'
    let carryOutput p v o s = match p s with s, r, o' -> s, r, o ++ o'

    //combinator operators
    let (.>) p1 p2 = combine p1 (carryOutput p2) //ignores value from p1 but carries stream output through
    let (.>>) p1 f = combine p1 f

    let fail state msg = failwithf "Parse error at index %d: %s" state.Index msg

    let skipWhitespace state = 
        let rec loop (state:State) =  
            match state.Current with
            | Some c when Char.IsWhiteSpace c -> loop state.Step
            | _ -> state
        loop state 

    ///skip any whitespace
    let rec p_ws : Parser<string> = 
        fun state ->
            let state = skipWhitespace state
            if state.Current = None then 
                state, Empty p_ws, None
            else 
                state, Done None, None

    ///recognize a single character
    let rec pchar (c:char) (s:State) =
        match s.Current with
        | None  -> s, Empty (pchar c), None
        | Some c' when c = c' -> s.Step, Done None, None
        | _ -> s, Fail $"pchar: Expected '{c}'", None

    let rec private _pcharlist (state:State) xs = 
        match state.Current, xs with 
        | _,  []                         -> state, Done None
        | Some c1, c2::rest when c1 = c2 -> _pcharlist state.Step rest
        | None, _                        -> state, Empty (pcharlist xs)
        | Some c1, c2::_                 -> state, Fail $"pcharlist: Expected char {c2} got {c1}"
    
    ///recognize a continuous sequence of characters    
    and pcharlist (xs:char list) (state:State) = 
        let s,o = _pcharlist state xs
        s, o, None

    ///recognize a string    
    let pstring (str:string) =  pcharlist (Seq.toList str) 

    //Supports finding the end of quoted a string that may span multiple chunks
    type SEnd = 
        | Continues     
        | Ends of int
        | Backslash

    let rec mapStringLoop (acc:Text.StringBuilder) (s:string) i = 
        if i >= s.Length then 
            acc.ToString(), Continues
        elif i = s.Length - 1 then
            if s.[i] = '\\' then 
                acc.ToString(), Backslash
            else 
                acc.Append s.[i] |> ignore
                acc.ToString(), Continues
        else 
            let a = s.[i]
            let b = s.[i+1]
            if a = '\\' && b = '"' then 
                acc.Append '"'              |> ignore
                mapStringLoop acc s (i+2)
            elif a = '\\' && b = '\\' then 
                acc.Append '\\'             |> ignore
                mapStringLoop acc s (i+2)
            elif a = '\\' && b = 'n' then 
                acc.Append '\n'             |> ignore
                mapStringLoop acc s (i+2)
            elif a = '\\' && b = 't' then 
                acc.Append '\t'             |> ignore
                mapStringLoop acc s (i+2)
            elif a = '\\' && b = 'r' then 
                acc.Append '\r'             |> ignore
                mapStringLoop acc s (i+2)
            elif b = '"' then 
                acc.Append(a).ToString(), Ends (i+2)
            else 
                acc.Append a                |> ignore
                mapStringLoop acc s (i+1)

    ///remove escape characters from a quoted string and find the end of the quoted string, if it exits
    let mapString (s:string) i = 
        if i >= s.Length then 
            "", Continues
        elif s.[i] = '"' then 
            "", Ends 1
        else 
            mapStringLoop (Text.StringBuilder()) s i

    /// stream out chunks of a quoted string as they are processed
    let rec p_strm_quoted_string (s:State) =
        match s.Current with 
        | None      -> s, Empty p_strm_quoted_string, None
        | Some '"'  -> p_strm_quoted_string_cont "" s.Step
        | Some c    -> fail s $"p_strm_quoted_string: Expected '\"' but got {c}"
    and p_strm_quoted_string_cont acc s =         
        match mapString s.SourceChunk s.Index with 
        | str,Continues      -> s, Empty (p_strm_quoted_string_cont ""), Some (acc + str)
        | str,Ends i         -> {s with Index=i}, Done None, Some (acc + str)               //take care to output the last chunk when combined with another parser
        | str,Backslash      -> s, Empty (p_strm_after_backslash ""), Some (acc + str)
    and p_strm_after_backslash acc (s:State) = 
        match s.Current with 
        | None                  -> s, Empty (p_strm_after_backslash acc), None
        | Some c when c = '"'   -> p_strm_quoted_string_cont (acc + "\"") s.Step
        | Some c when c = '\\'  -> p_strm_quoted_string_cont (acc + "\\") s.Step
        | Some c when c = 'n'   -> p_strm_quoted_string_cont (acc + "\n") s.Step
        | Some c when c = 't'   -> p_strm_quoted_string_cont (acc + "\t") s.Step
        | _                     -> p_strm_quoted_string_cont acc s                  //no step as that will be handled by the next call

    ///internally collect a complete quoted string (don't stream out chunks)
    let rec p_quoted_string (s:State) =
        match s.Current with 
        | None      -> s, Empty p_quoted_string, None
        | Some '"'  -> p_quoted_string_cont "" s.Step
        | Some c    -> fail s $"p_quoted_string: Expected '\"' but got {c}"
    and p_quoted_string_cont acc s =         
        match mapString s.SourceChunk s.Index with 
        | str,Continues      -> s, Empty (p_quoted_string_cont (acc + str)), None
        | str,Ends i         -> {s with Index=i}, Done (Some (acc + str)), None
        | str,Backslash      -> s, Empty (p_after_backslash (acc + str)), None
    and p_after_backslash acc (s:State) = 
        match s.Current with 
        | None                  -> s, Empty (p_after_backslash acc), None
        | Some c when c = '"'   -> p_quoted_string_cont (acc + "\"") s.Step
        | Some c when c = '\\'  -> p_quoted_string_cont (acc + "\\") s.Step
        | Some c when c = 'n'   -> p_quoted_string_cont (acc + "\n") s.Step
        | Some c when c = 't'   -> p_quoted_string_cont (acc + "\t") s.Step
        | _                     -> p_quoted_string_cont acc s

    let inline makeList v = match v with Some x -> Some [x] | _ -> None

    ///parse a json-style list of quoted strings
    let rec p_string_list : Parser<string list> =
        fun (s:State) ->
            match s.Current with 
            | None -> s, Empty p_string_list, None
            | Some '[' -> p_string_list_cont [] s.Step
            | Some c -> fail s "p_string_list: Expected '[' but got {c}"
    and p_string_list_cont acc (s:State) = 
        match s.Current with
        | None                              -> s, Empty (p_string_list_cont acc), None
        | Some ']'                          -> s.Step, Done (Some (List.rev acc)), None
        | Some c when Char.IsWhiteSpace c   -> p_string_list_cont acc s.Step                        //skip whitespace
        | Some c when c = ','               -> p_string_list_cont acc s.Step                        //assumes llm produces valid json list with interspersed commas 
        | Some '"'                          -> 
            match p_quoted_string s with
            | s, Done str, _   ->  p_string_list_cont (append acc str) s
            | s, Empty p, o    -> s, Empty (p .>>  (fun v o -> p_string_list_cont (append acc v))), o
            | s, Fail msg, o   -> s, Fail msg, o
        | _                                 -> fail s $"p_string_list: Expected '\"' or ']' or ',' got {s.Current}"

    //above is mostly generic, below is specific to response json
    
    let p_brace1 : Parser<string>    = pchar '{' 
    let p_brace2 : Parser<string>    = pchar '}'
    let p_colon : Parser<string>     = pchar ':'
    let p_comma : Parser<string>     = pchar ','
    let p_citations : Parser<string> = pstring "\"CitationIds\""
    let p_answer : Parser<string>    = pstring "\"Answer\""
    let inline p_done s              = s, Done None, None

    ///capture the citations list to an external reference
    let setCitsValue (cits:string list ref) xs : string option = 
        match xs with 
        | Some xs -> printfn "%A" xs; cits.Value <- xs
        | None -> ()
        None

    let p_citations_list (cits:Ref<string list>) : Parser<string> = (map (setCitsValue cits) p_string_list) 

    //expression to parse response json to extract citations and stream out the answer
    let exp cits = 
        p_ws .> p_brace1 .> p_ws 
        .> p_citations .> p_ws .> p_colon .> p_ws 
        .> (p_citations_list cits) .> p_ws
        .> p_comma .> p_ws
        .> p_answer .> p_ws .> p_colon .> p_ws
        .> p_strm_quoted_string .> p_ws .> p_brace2

    let inline d s r = printfn "%A" (s,r)

    let rec step (p,(s,acc)) = 
        match  p s with 
        | s, Done _, o          -> p_done, (s,append acc o)
        | s, Fail msg, _        -> fail s msg
        | s, Empty p, o         -> p, (s, append acc o)

    let updateState (p,(s:State,acc)) str = step (p,(s.NextChunk str,[]))

    let test source =
        let cits = ref []
        ((exp cits,(State.Empty,[])),source)
        ||> Seq.scan updateState
        |> Seq.collect (fun (_,(_,os)) -> List.rev os)
        |> Seq.iter (printfn "%s")
