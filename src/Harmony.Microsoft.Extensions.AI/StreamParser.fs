namespace Harmony.Microsoft.Extensions.AI
open System

/// <summary>
/// Input structure for supplying RAG content with citations ids.<br />
/// With the right prompt, the LLM should return the citation ids of the
/// content that it used to generate the answer. See also <see cref="AnswerWithCitations"/>. 
/// </summary>
type Citation =
    {
        Id: string
        Title: string
        Text: string
    }
    
/// <summary>
/// LLM structured output schema for an 'answer with citations'.<br />
/// LLM responds with this structure in a streaming fashion.<br />
/// A StreamParser 'grammar' can be used to stream the Answer part out to app (chatbot).<br />
/// When the stream is done, the citations is obtained as a separate list.
/// Even though LLM is structured, it can still be dealt with in a streaming fashion.
/// </summary>
type AnswerWithCitations =
    {
        CitationIds : string list
        Answer : string
    }

///Monadic parser to handle streaming 'structured' content with a supplied grammar. 
module StreamParser =
    let private checkEmpty (s:string) =
        if String.IsNullOrWhiteSpace s then None else Some s

    type State =
        {
            SourceChunk : string
            Index : int
        }
        with
            static member Empty = {SourceChunk = ""; Index = 0 }
            member this.Step = { this with Index = this.Index + 1 }
            member this.Current = if this.Index >= this.SourceChunk.Length then None else Some this.SourceChunk.[this.Index]
            member this.NextChunk s = { this with SourceChunk = s; Index = 0 }
            member this.ConsumedFrom i =                  
                if i > this.Index then failwith "invalid index" 
                this.SourceChunk.Substring(i,this.Index-i)
            member this.Drain() = 
                if this.Index < this.SourceChunk.Length then 
                    this.SourceChunk.Substring(this.Index), {this with Index=this.SourceChunk.Length-1}
                else 
                    "", this
            
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

    let ret a = fun (s:State) -> s, Done (Some a), None

    let rec map f (p:Parser<'a>) (s:State) =
        match p s with
        | s, Done v, o    -> s, Done (f v), o
        | s, Fail msg,  o -> s, Fail msg,          o
        | s, Empty p,   o -> s, Empty (map f p),   o

    ///Combine two parsers in sequence
    let rec bind (p1:Parser<'a>) (f: 'a option -> string option -> Parser<'b>) s =
        match p1 s with
        | s, Done v, o     -> (f v o) s
        | s, Fail msg, o   -> s, Fail msg, o
        | s, Empty p1, o   -> s, Empty (bind p1 f), o

    let (++) (s:string option) (o:string option) = match s, o   with Some s, Some o -> Some (s + o) | None, Some p | Some p, None -> Some p | _ -> None

    //carry along any intermediate stream output till its bubbled up to the top for release
    //let carryOutput p v o = match o with None -> p | o -> fun s -> match p s with s, r, o' -> s, r, o ++ o'
    let carryOutput p v o s = match p s with s, r, o' -> s, r, o ++ o'

    //combinator operators
    let (.>) p1 p2 = bind p1 (carryOutput p2) //ignores value from p1 but carries stream output through
    let (.>>) p1 f = bind p1 f

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
        | _ -> s, Fail (sprintf "pchar: Expected '%c'" c), None

    let rec private _pcharlist (state:State) xs =
        match state.Current, xs with
        | _,  []                         -> state, Done None
        | Some c1, c2::rest when c1 = c2 -> _pcharlist state.Step rest
        | None, _                        -> state, Empty (pcharlist xs)
        | Some c1, c2::_                 -> state, Fail (sprintf "pcharlist: Expected char %c got %c" c2 c1)

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
        | Some c    -> fail s (sprintf "p_strm_quoted_string: Expected '\"' but got %c" c)
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
        | Some c    -> fail s (sprintf "p_quoted_string: Expected '\"' but got %c" c)
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
            | Some 'n' -> p_null s
            | Some c -> fail s (sprintf "p_string_list: Expected '[' but got %c" c)
    and p_null (s:State) = map (fun _ -> Some [])  (pstring "null") s 
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
        | _                                 ->
            let got =
                match s.Current with
                | Some ch -> sprintf "%c" ch
                | None -> "EOF"
            fail s (sprintf "p_string_list: Expected '\"' or ']' or ',' got %s" got)

    let  rec private _accumTill acc accT o (p:Parser<string>) (state:State) =
        match p state with 
        | s, Done v, o -> s, Done (Some (List.rev acc  |> String.concat "")), o
        | s, Empty p, o -> s, Empty (_accumTill acc (s.ConsumedFrom state.Index::accT) None p), o
        | s, Fail _, o' -> let s = s.Step in _accumTill (s.ConsumedFrom state.Index::(accT @ acc )) [] (o ++ o') p s

    ///Accumulate characters till the parser p succeeds. Consumes input till end of p.
    let accumTill (p:Parser<string>) (state:State) =
        _accumTill [] [] None p state

    ///Stream out contents as they are received
    let rec p_strm_any_string (s:State) = 
        let c,s = s.Drain()
        s, Empty p_strm_any_string, Some c

    let inline p_done s              = s, Done None, None

    //api

    let rec step (p,(s,acc)) =
        match  p s with
        | s, Done _, o          -> p_done, (s,append acc o)
        | s, Fail msg, _        -> fail s msg
        | s, Empty p, o         -> p, (s, append acc o)

    let updateState (p,(s:State,acc)) str = step (p,(s.NextChunk str,[]))


(*
--------- Above is generic parsing code ----------
Below are specific grammars
*)

module CitationsGrammar =
    open StreamParser
    //Parse Citations
    
    let p_brace1 : Parser<string>    = pchar '{'
    let p_brace2 : Parser<string>    = pchar '}'
    let p_colon : Parser<string>     = pchar ':'
    let p_comma : Parser<string>     = pchar ','
    let p_citations : Parser<string> = pstring "\"CitationIds\""
    let p_answer : Parser<string>    = pstring "\"Answer\""

    ///capture the citations list to an external reference
    let setCitsValue (cits:string list ref) xs : string option =
        match xs with
        | Some xs -> printfn "%A" xs; cits.Value <- xs
        | None -> ()
        None

    let p_citations_list (cits:Ref<string list>) : Parser<string> = (map (setCitsValue cits) p_string_list)

    //expression to parse response json to extract citations and stream out the answer
    let citationsExp cits =
        p_ws .> p_brace1 .> p_ws
        .> p_citations .> p_ws .> p_colon .> p_ws
        .> (p_citations_list cits) .> p_ws
        .> p_comma .> p_ws
        .> p_answer .> p_ws .> p_colon .> p_ws
        .> p_strm_quoted_string .> p_ws .> p_brace2


// testing helpers

    let testCitations source =
        let cits = ref []
        ((citationsExp cits,(State.Empty,[])),source)
        ||> Seq.scan updateState
        |> Seq.collect (fun (_,(_,os)) -> List.rev os)
        |> Seq.iter (printfn "%s")

module HarmonyGrammar =
    open StreamParser
    //Parse Harmony format think tokens

    let private checkEmpty (s:string) =
        if String.IsNullOrWhiteSpace s then None else Some s

    let p_channel : Parser<string> = pstring "<|channel|>"
    let p_message : Parser<string> = pstring "<|message|>"
    let p_analysis : Parser<string> = pstring "analysis"
    let p_end : Parser<string> = pstring "<|end|>"

    let p_thought (thought:Ref<string>) : Parser<string> = 
        map (function Some v -> thought.Value <- v; Some v | _ -> None) (accumTill p_end)

    ///expression to stream parse harmonay 'think tokens' <|channel|>analysis<|message|>...<|end|>
    let harmonyExp thought =    
        p_ws .> p_channel .> p_ws .> p_analysis .> p_ws .> p_message
        .> (p_thought thought) .> p_strm_any_string

    let splitHarmony (text:string) =
        let analysisPrefix = "<|channel|>analysis<|message|>"
        let finalPrefix = "<|channel|>final<|message|>"
        let endTag = "<|end|>"

        let trimFinalPrefix (value:string) =
            let trimmed = value.Trim()
            if trimmed.StartsWith(finalPrefix, StringComparison.Ordinal) then
                trimmed.Substring(finalPrefix.Length).Trim()
            else
                trimmed

        if text.Contains(analysisPrefix, StringComparison.Ordinal) then
            let startIdx = text.IndexOf(analysisPrefix, StringComparison.Ordinal) + analysisPrefix.Length
            let endIdx = text.IndexOf(endTag, startIdx, StringComparison.Ordinal)
            let thoughts =
                if endIdx >= 0 then text.Substring(startIdx, endIdx - startIdx).Trim()
                else text.Substring(startIdx).Trim()
            let remainder =
                if endIdx >= 0 then
                    text.Substring(endIdx + endTag.Length)
                    |> trimFinalPrefix
                else
                    ""
            let output =
                match checkEmpty remainder with
                | Some value -> value
                | None -> thoughts
            output, checkEmpty thoughts
        elif text.Contains(finalPrefix, StringComparison.Ordinal) then
            let startIdx = text.IndexOf(finalPrefix, StringComparison.Ordinal) + finalPrefix.Length
            let output = text.Substring(startIdx).Trim()
            output, None
        else
            text.Trim(), None

