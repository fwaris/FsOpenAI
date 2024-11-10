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

    type Output<'a> = Empty of Parser<'a> | Next of 'a * Parser<'a> option | Fail of string
    and Parser<'a> = State -> State * Output<'a> * string option

    let rec combine (p1:Parser<'a>) (p2:Parser<'a>) s = 
        match p1 s with 
        | s, Next (v,Some p1), o -> s, Next (v,Some (combine p1 p2)), o
        | s, Next (v,None), o    -> s, Next (v,Some p2), o
        | s, Empty p1, o         -> s, Empty (combine p1 p2), o
        | s, Fail msg, _         -> s, Fail msg, None

    let rec map (p1:Parser<'a>) (p2:Parser<'b>) f s = 
        match p1 s with 
        | s, Next (v,Some p1), o  -> s, Next (f v,Some (map p1 p2 f)), o
        | s, Next (v,None), o     -> s, Next (f v,Some p2), o
        | s, Empty p1, o          -> s, Empty (map p1 p2 f), o
        | s, Fail msg, _          -> s, Fail msg, None

    let (.>) p1 p2 = combine p1 p2
    let (.>>) p1 (f,p2) = map p1 p2 f

    let fail state msg = failwithf "Parse error at index %d: %s" state.Index msg

    let skipWhitespace state = 
        let rec loop (state:State) =  
            match state.Current with
            | Some c when Char.IsWhiteSpace c -> loop state.Step
            | _ -> state
        loop state 

    let rec p_ws : Parser<string option> = 
        fun state ->
            let state = skipWhitespace state
            if state.Current = None then 
                state, Empty p_ws, None
            else 
                state, Next (None,None), None

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
    
    //look for a continuous sequence of characters
    and pcharlist (xs:char list) (state:State) = 
        let s,o = _pcharlist state xs
        s, o, None

    let pstring (str:string) =  pcharlist (Seq.toList str) 

    type SEnd = 
        | Continues      //quoted string does not end in this chunk
        | Ends of int    //quoted string ends in this chunk at this index
        | Backslash
        
    let rec findFirstUnescapedQoute (s:string) i = 
        if i >= s.Length then -1
        else 
            let quote = s.IndexOf('"',i)
            if quote = -1 then -1
            elif quote = 0 then 0
            elif s.[quote - 1] = '\\' then findFirstUnescapedQoute s (quote + 1)
            else quote

    let stringEnd (s:string) i = 
        match findFirstUnescapedQoute s i, s.EndsWith('\\') with
        | -1,true -> Backslash   //no end quote found but string ends with a backslash (consider that for next chunk)
        | -1,false -> Continues  //no quote found and string does not end with backslash
        | i, _     -> Ends i     //end quote found at i

    /// stream out chunks of quoted string as they are processed
    let rec p_strm_quoted_string (s:State) =
        match s.Current with 
        | None      -> s, Empty p_strm_quoted_string, None
        | Some '"'  -> s.Step, Next(None,Some p_strm_quoted_string_cont), (Some "\"")
        | _         -> fail s "p_quoted_string: Expected '\"'"
    and p_strm_quoted_string_cont s =         
        match stringEnd s.SourceChunk s.Index with 
        | Continues      -> s, Empty p_strm_quoted_string_cont, Some (s.SourceChunk.Substring(s.Index))
        | Ends i         -> {s with Index=i+1}, Next (None,None), Some (s.SourceChunk.Substring(s.Index,i-s.Index))
        | Backslash      -> s, Empty p_strm_after_backslash, Some (s.SourceChunk.Substring(s.Index))
    and p_strm_after_backslash (s:State) = 
        match s.Current with 
        | None                  -> s, Empty p_strm_after_backslash, None
        | Some c when c = '"'   -> s.Step, Next(None, Some (p_strm_quoted_string_cont)), (Some "\"")
        | _                     -> s,Next(None,Some (p_strm_quoted_string_cont)), None

    ///internally collect a compplete quoted string (don't stream out chunks)
    let rec p_quoted_string (s:State) =
        match s.Current with 
        | None      -> s, Empty p_quoted_string, None
        | Some '"'  -> s.Step, Next("",Some (p_quoted_string_cont "")), None
        | _         -> fail s "p_quoted_string: Expected '\"'"
    and p_quoted_string_cont acc s =         
        match stringEnd s.SourceChunk s.Index with 
        | Continues      -> s, Empty (p_quoted_string_cont (acc + s.SourceChunk.Substring(s.Index))), None
        | Ends i         -> {s with Index=i+1}, Next (acc + s.SourceChunk.Substring(s.Index,i-s.Index),None), None
        | Backslash      -> s, Empty (p_after_backslash (acc + s.SourceChunk.Substring(s.Index))), None
    and p_after_backslash acc (s:State) = 
        match s.Current with 
        | None                  -> s, Empty (p_after_backslash acc), None
        | Some c when c = '"'   -> s.Step, Next("", Some (p_quoted_string_cont (acc + "\""))), None
        | _                     -> s,Next("",Some (p_quoted_string_cont acc)), None

    let rec p_string_list_cont acc (s:State) = 
        match s.Current with
        | None                              -> s, Empty (p_string_list_cont acc), None
        | Some ']'                          -> s.Step, Next(List.rev acc,None),None
        | Some c when Char.IsWhiteSpace c   -> s.Step, Next(acc, Some(p_string_list_cont acc)), None
        | Some c when c = ','               -> s.Step, Next(acc, Some(p_string_list_cont acc)), None
        | Some '"'                          -> 
            match p_quoted_string s.Step with
            | s, Next(str,None), o   -> s, Next([], Some(p_string_list_cont (str::acc))), o
            | s, Next(_,Some p), o   -> s, Next([], Some(p .>> ((fun x->[x]), p_string_list_cont acc))), o
            | s, Empty p, o          -> s, Next([], Some(p .>> ((fun x->[x]), p_string_list_cont acc))), o
            | s, Fail msg, o         -> s, Fail msg, o
        | _                                 -> fail s $"p_string_list: Expected '\"' or ']' or ',' got {s.Current}"

    let rec p_string_list (s:State) = 
        match s.Current with 
        | None -> s, Empty p_string_list, None
        | Some '[' -> s.Step, Next([], Some (p_string_list_cont [])), None
        | _ -> fail s "p_string_list: Expected '['"

    //above is generic, below is specific to response json
    
    let p_brace1  = pchar '{' 
    let p_brace2  = pchar '}'
    let p_bracket = pchar '['
    let p_done s = s, Next(None,None), None
    let p_colon = pchar ':'
    let p_comma = pchar ','
    let p_quote : Parser<string option> = pchar '"'
    let p_citations = pstring "\"Citations\""
    let p_answer = pstring "\"Answer\""

    let p_citations_list (cits:Ref<string list>) = p_string_list .>> ((fun xs -> cits.Value <- xs; None), p_ws)

    //expression to parse response json to extract citations and stream out the answer
    let exp cits = 
        p_ws .> p_brace1 .> p_ws 
        .> p_citations .> p_ws .> p_colon .> p_ws 
        .> p_bracket .> p_ws .> (p_citations_list cits)
        .> p_comma .> p_ws
        .> p_answer .> p_ws .> p_colon .> p_ws
        .> p_strm_quoted_string .> p_ws .> p_brace2

    let inline d s r = printfn "%A" (s,r)

    let append acc o = match o with Some s -> s::acc | _ -> acc

    let rec step (p,(s,acc)) = 
        match  p s with 
        | s, Next(_,None), _    -> d s "Done"; p_done, (s,[])
        | s, Next(_,Some p), o  -> d s "Next"; step (p, (s, append acc o))
        | s, Fail msg, _        -> fail s msg
        | s, Empty p, o         -> d s "Empty"; p, (s, append acc o)

    let updateState (p,(s:State,acc)) str = step (p,(s.NextChunk str,acc))

    let test source =
        let cits = ref []
        ((exp cits,(State.Empty,[])),source)
        ||> Seq.scan updateState
        |> Seq.collect (fun (_,(_,os)) -> List.rev os)
        |> Seq.iter (printfn "%s")
