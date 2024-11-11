namespace FsOpenAI.GenAI
open System

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
        | Next of 'a option * Parser<'a> option //optional value and optional continuation. The value may be consumed by a parent parser. The continuation is the next parser to run
        | Fail of string                        //failed to parse

    let inline append acc o = match o with Some s -> s::acc | _ -> acc

    let bind a = fun (s:State)-> s, Next(Some a,None), None

    let rec map f (p:Parser<'a>) : Parser<'b>= 
        fun (s:State) -> 
            match p s with 
            | s, Next(v,None), o    -> s, Next(f v,None),               o 
            | s, Next(_,Some p), o  -> s, Next(None,Some (map f p)),    o 
            | s, Fail msg,  o       -> s, Fail msg,                     o
            | s, Empty p,   o       -> s, Empty (map f p),              o

    ///Combine two parsers in sequence
    let rec combine (p1:Parser<'a>) (p2:Parser<'a>) s = 
        match p1 s with 
        | s, Next (_,Some p1), o -> s, Next (None,Some (combine p1 p2)), o
        | s, Next (v,None), o    -> s, Next (v,Some p2), o
        | s, Empty p1, o         -> s, Empty (combine p1 p2), o
        | s, Fail msg, _         -> s, Fail msg, None

    //combinator operator
    let (.>) p1 p2 = combine p1 p2

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
                state, Next (None,None), None

    ///recognize a single character
    let rec pchar (c:char) (s:State) =
        match s.Current with
        | None  -> s, Empty (pchar c), None
        | Some c' when c = c' -> s.Step, Next (None,None), None
        | _ -> s, Fail $"pchar: Expected '{c}'", None

    let rec private _pcharlist (state:State) xs = 
        match state.Current, xs with 
        | _,  []                         -> state, Next (None,None) 
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
        | Some '"'  -> s.Step, Next(None,Some p_strm_quoted_string_cont), None
        | Some c    -> fail s $"p_strm_quoted_string: Expected '\"' but got {c}"
    and p_strm_quoted_string_cont s =         
        match mapString s.SourceChunk s.Index with 
        | str,Continues      -> s, Empty p_strm_quoted_string_cont, Some str
        | str,Ends i         -> {s with Index=i}, Next (None,None), Some str
        | str,Backslash      -> s, Empty p_strm_after_backslash, Some str
    and p_strm_after_backslash (s:State) = 
        match s.Current with 
        | None                  -> s, Empty p_strm_after_backslash, None
        | Some c when c = '"'   -> s.Step, Next(None, Some (p_strm_quoted_string_cont)), (Some "\"")
        | Some c when c = '\\'  -> s.Step, Next(None, Some (p_strm_quoted_string_cont)), (Some "\\")
        | Some c when c = 'n'   -> s.Step, Next(None, Some (p_strm_quoted_string_cont)), (Some "\n")
        | Some c when c = 't'   -> s.Step, Next(None, Some (p_strm_quoted_string_cont)), (Some "\t")
        | _                     -> s,Next(None,Some (p_strm_quoted_string_cont)), None

    ///internally collect a complete quoted string (don't stream out chunks)
    let rec p_quoted_string (s:State) =
        match s.Current with 
        | None      -> s, Empty p_quoted_string, None
        | Some '"'  -> s.Step, Next(None,Some (p_quoted_string_cont "")), None
        | Some c    -> fail s $"p_quoted_string: Expected '\"' but got {c}"
    and p_quoted_string_cont acc s =         
        match mapString s.SourceChunk s.Index with 
        | str,Continues      -> s, Empty (p_quoted_string_cont (acc + str)), None
        | str,Ends i         -> {s with Index=i}, Next (Some (acc + str),None), None
        | str,Backslash      -> s, Empty (p_after_backslash (acc + str)), None
    and p_after_backslash acc (s:State) = 
        match s.Current with 
        | None                  -> s, Empty (p_after_backslash acc), None
        | Some c when c = '"'   -> s.Step, Next(None, Some (p_quoted_string_cont (acc + "\""))), None
        | Some c when c = '\\'  -> s.Step, Next(None, Some (p_quoted_string_cont (acc + "\\"))), None
        | Some c when c = 'n'   -> s.Step, Next(None, Some (p_quoted_string_cont (acc + "\n"))), None
        | Some c when c = 't'   -> s.Step, Next(None, Some (p_quoted_string_cont (acc + "\t"))), None
        | _                     -> s,Next(None,Some (p_quoted_string_cont acc)), None

    let inline makeList v = match v with Some x -> Some [x] | _ -> None

    ///parse a json-style list of quoted strings
    let rec p_string_list (s:State) = 
        match s.Current with 
        | None -> s, Empty p_string_list, None
        | Some '[' -> s.Step, Next(None, Some (p_string_list_cont [])), None
        | Some c -> fail s "p_string_list: Expected '[' but got {c}"
    and p_string_list_cont acc (s:State) = 
        match s.Current with
        | None                              -> s, Empty (p_string_list_cont acc), None
        | Some ']'                          -> s.Step, Next(Some(List.rev acc),None),None
        | Some c when Char.IsWhiteSpace c   -> s.Step, Next(None, Some(p_string_list_cont acc)), None
        | Some c when c = ','               -> s.Step, Next(None, Some(p_string_list_cont acc)), None
        | Some '"'                          -> 
            match p_quoted_string s with
            | s, Next(str,None), o   -> s, Next(None, Some(p_string_list_cont (append acc str))), o
            | s, Next(str,Some p), o -> s, Next(None, Some((map makeList p) .> (p_string_list_cont (append acc str)))), o
            | s, Empty p, o          -> s, Next(None, Some((map makeList p) .> (p_string_list_cont acc))), o
            | s, Fail msg, o         -> s, Fail msg, o
        | _                                 -> fail s $"p_string_list: Expected '\"' or ']' or ',' got {s.Current}"

    //above is generic, below is specific to response json
    
    let p_brace1  = pchar '{' 
    let p_brace2  = pchar '}'
    let p_done s = s, Next(None,None), None
    let p_colon = pchar ':'
    let p_comma = pchar ','
    let p_citations = pstring "\"Citations\""
    let p_answer = pstring "\"Answer\""

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
        | s, Next(_,None), o    -> p_done, (s,append acc o)
        | s, Next(_,Some p), o  -> step (p, (s, append acc o))
        | s, Fail msg, _        -> fail s msg
        | s, Empty p, o         -> p, (s, append acc o)

    let updateState (p,(s:State,acc)) str = step (p,(s.NextChunk str,[]))

    let test source =
        let cits = ref []
        ((exp cits,(State.Empty,[])),source)
        ||> Seq.scan updateState
        |> Seq.collect (fun (_,(_,os)) -> List.rev os)
        |> Seq.iter (printfn "%s")
