namespace FsOpenAI.GenAI
open System

module StreamParser = 

//#load "ScriptEnv.fsx"

//let source = (System.IO.File.ReadAllLines("C:/temp/chat.text")) |> Seq.toList

    type State = 
        {
            Citations : string list
            SourceChunk : string
            Index : int
        }
        with 
            member this.Step = { this with Index = this.Index + 1 }
            member this.Current = if this.Index >= this.SourceChunk.Length then None else Some this.SourceChunk.[this.Index] 
            // member this.Next s = { this with SourceChunk = s; Index = 0 }
            // member this.Rest = if this.Current.IsNone then "" else this.SourceChunk.Substring(this.Index)
            static member Empty = { Citations = []; SourceChunk = ""; Index = 0 }

    let fail state msg = failwithf "Parse error at index %d: %s" state.Index msg

    (*
    a streaming parser is a list of functions that can be applied to a state
    lower level parsers can be combined to higher level parsers using combinators
    each parser can output 3 things - new state, Output, optional string
    Output can be one of the following:
        Empty parser - current chunk was consumed so continue from the given parser but wait for a new chunk to arrive
        Cont  parser - part of the current chunk was consumed so continue from the given parser for the remaining chunk
        Done         - parsing is complete
        Fail         - parsing failed with error message
    *)

    type Output = Empty of Parser | Cont of Parser | Done | Fail of string
    and Parser = State -> State * Output * string option

    let skipWhitespace state = 
        let rec loop (state:State) =  
            match state.Current with
            | Some c when Char.IsWhiteSpace c -> loop state.Step
            | _ -> state
        loop state 

    let rec p_ws (state:State) = 
        let state = skipWhitespace state
        if state.Current = None then 
            state, Empty p_ws, None
        else 
            state, Done, None

    let rec pchar (c:char) (s:State) =
        match s.Current with
        | None  -> s, Empty (pchar c), None
        | Some c' when c = c' -> s.Step, Done, None
        | _ -> s, Fail $"pchar: Expected '{c}'", None

    //look for a continuous sequence of characters
    let rec pcharlist (xs:char list) (state:State) = 
        let rec loop (state:State) xs = 
            match state.Current, xs with 
            | _,  [] -> state, Done
            | Some c1, c2::rest when c1 = c2 -> loop state.Step rest
            | None, _ -> state, Empty (pcharlist xs)
            | _, _ -> state, Fail "pcharlist: Expected char list"
        let s,o = loop state xs
        s, o, None

    let pstring (str:string) =  pcharlist (Seq.toList str) 

    type SEnd = 
        | Continues         //quoted string does not end in this chunk
        | Ends of string    //quoted string ends in this chunk, here is the remaining string
        | Backslash         //current chunk ends with a backslash, treat first char of next chunk as part of this string

    let stringEnd (s:string) = 
        if s.Length = 0 then "", Continues
        else 
            let lastQuote = s.LastIndexOf('"')
            if lastQuote = -1 then s, Continues
            elif lastQuote = 0 then s, Ends ""
            elif s.[lastQuote - 1] = '\\' then s, Backslash
            elif lastQuote = s.Length - 1 then s, Ends ""
            else s.Substring(0, lastQuote+1), Ends (s.Substring(lastQuote + 1))

    let rec p_any_string s = 
        match stringEnd s.SourceChunk with 
        | _, Continues   -> s, Empty p_any_string, Some s.SourceChunk
        | str, Ends rest -> {s with SourceChunk=rest; Index=0}, Done, Some str
        | _, Backslash     -> s, Empty p_any_string, Some s.SourceChunk
    and p_after_backslash (s:State) = 
        match s.Current with 
        | None -> s, Empty p_after_backslash, None
        | Some c when c = '"' -> s.Step,Cont p_any_string, (Some "\"")
        | _ -> s,Cont p_any_string, None

    let rec combine p1 p2 s = 
        match p1 s with 
        | s, Done, o -> printfn "done p1"; s, Cont p2, o
        | s, Cont p1, o -> s, Cont (combine p1 p2), o
        | s, Empty p1, o -> s, Empty (combine p1 p2), o
        | s, r, o    -> s,r,o 

    let (.>) p1 p2 = combine p1 p2

    let p_brace1  = pchar '{' 
    let p_brace2  = pchar '}'
    let p_bracket = pchar '['
    let p_done s = s, Done, None
    let p_colon = pchar ':'
    let p_comma = pchar ','
    let p_quote = pchar '"'

    //specific to response json

    let p_citations = pstring "\"Citations\""
    let p_answer = pstring "\"Answer\""

    let (++) acc c = match c with Some s -> acc + s | _ -> acc

    let rec p_citations_list (s:State) = 
        match s.Current with
        | None                              -> s, Empty p_citations_list, None
        | Some ']'                          -> s.Step, Done, None
        | Some '"'                          -> s.Step, Cont (p_citations_string ""), None    
        | Some c when Char.IsWhiteSpace c   -> s.Step, Cont p_citations_list, None
        | Some c when c = ','               -> s.Step, Cont p_citations_list, None
        | _                                 -> fail s $"p_citations_list: Expected '\"' or ']' or ',' got {s.Current}"

    and p_citations_string acc (s:State) = 
        match s.Current with
        | None -> s, Empty (p_citations_string acc), None
        | _ -> 
            match p_any_string s with 
            | s, Done, chnk     -> {s with Citations= (acc ++ chnk) :: s.Citations}, Cont p_citations_list, None
            | s, Cont p, chnk   -> s, Cont (p .> p_citations_string (acc ++ chnk)), None
            | s, Empty p, chnk  -> s, Empty (p .> p_citations_string (acc ++ chnk)), chnk
            | s, Fail msg, _    -> fail s $"p_citations: {msg}"

    let exp = p_ws .> p_brace1 .> p_ws 
             .> p_citations .> p_ws .> p_colon .> p_ws 
             .> p_bracket .> p_citations_list .> p_ws 
             .> p_comma .> p_ws
             .> p_answer .> p_ws .> p_colon .> p_ws .> p_quote
             .> p_any_string .> p_ws .> p_brace2

    let inline d s r = printfn "%A" (s,r)

    let append acc o = match o with Some s -> s::acc | _ -> acc

    let rec step (p,(s,_,acc)) = 
        match  p s with 
        | s, Done, o        -> d s Done; p_done, (s,Done, append acc o)
        | s, Fail msg, _    -> fail s msg
        | s, Empty p, o     -> d s "Empty"; p,(s,Done, append acc o)
        | s, Cont p, o      -> d s "Cont";  step (p, (s,Done,acc))

    let updateState (p,(s,o,acc)) str = step (p,({s with SourceChunk = str},o,acc))

    let test source =
        ((exp,(State.Empty,Done,[])),source)
        ||> Seq.scan updateState
        |> Seq.collect (fun (_,(_,_,os)) -> List.rev os)
        |> Seq.iter (printfn "%s")

