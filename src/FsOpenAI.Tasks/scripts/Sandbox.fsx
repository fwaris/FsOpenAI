//#load "ScriptEnv.fsx"
open System

let source = System.Text.Json.JsonSerializer.Deserialize<string list>(System.IO.File.ReadAllText("C:/temp/chat.json"))

type State = 
    {
        Citations : string list
        SourceChunk : string
        Index : int
    }
    with 
        member this.Step = { this with Index = this.Index + 1 }
        member this.Current = if this.Index >= this.SourceChunk.Length then None else Some this.SourceChunk.[this.Index] 
        member this.Next s = { this with SourceChunk = s; Index = 0 }
        member this.Rest = if this.Current.IsNone then "" else this.SourceChunk.Substring(this.Index)
        static member Empty = { Citations = []; SourceChunk = ""; Index = 0 }

let fail state msg = failwithf "Parse error at index %d: %s" state.Index msg

(*
a streaming parser is a list of functions that can be applied to a state
lower level parsers can be combined to higher level parsers using combinators
each parser can output 3 things - new state, Empty of parser | Cont | Fail of string, and output string
Empty - current chunk was consumed so continue from the same parser 
Cont  - current chunk was consumed so continue to next parser with remaining chunk
Done  - parsing is complete
Fail  - parsing failed with error message
*)

type Output = Empty of Parser | Cont of Parser | Done | Fail of string
and Parser = State -> State * Output * string option

let skipWhitespace state = 
    let rec loop (state:State) =  
        match state.Current with
        | Some c when Char.IsWhiteSpace c -> loop state.Step
        | _ -> state
    loop state 

let rec pws (state:State) = 
    let state = skipWhitespace state
    if state.Current = None then 
        state, Empty pws, None
    else 
        state, Done, None

let rec pchar (c:char) (s:State) =
    match s.Current with
    | None  -> s, Empty (pchar c), None
    | Some c' when c = c' -> s.Step, Done, None
    | _ -> s, Fail $"Expected '{c}'", None

//look for a continuous sequence of characters
let rec pcharlist (xs:char list) (state:State) = 
    let rec loop (state:State) xs = 
        match state.Current, xs with 
        | _,  [] -> state, Done
        | Some c1, c2::rest when c1 = c2 -> loop state.Step rest
        | None, _ -> state, Empty (pcharlist xs)
        | _, _ -> state, Fail "Expected char list"
    let s,o = loop state xs
    s, o, None

let pstring (str:string) =  pcharlist (Seq.toList str) 

let rec combine p1 p2 s = 
    match p1 s with 
    | s, Done, o -> printfn "done p1"; s, Cont p2, o
    | s, Cont p1, o -> s, Cont (combine p1 p2), o
    | s, Empty p1, o -> s, Empty (combine p1 p2), o
    | s, r, o    -> s,r,o 

let (<|>) p1 p2 = combine p1 p2

let pbrace1  = pchar '{' 
let pbrace2  = pchar '}'
let pcitations = pstring "\"Citations\""
let panswer = pstring "\"Answer\""
let pbracket = pchar '['
let pNone s = s, Done, None
let pcolon = pchar ':'
let pcomma = pchar ','
let pquote = pchar '"'

type SEnd = 
    | Continues         //quoted string does not end in this chunk
    | Ends of string    //quoted string ends in this chunk, here is the remaining string
    | Partial           //quoted string may end as the char is backslash (escape char)

let stringEnd (s:string) = 
    if s.Length = 0 then "", Continues
    else 
        let lastQuote = s.LastIndexOf('"')
        if lastQuote = -1 then s, Continues
        elif lastQuote = 0 then s, Ends ""
        elif s.[lastQuote - 1] = '\\' then s, Partial
        elif lastQuote = s.Length - 1 then s, Ends ""
        else s.Substring(0, lastQuote+1), Ends (s.Substring(lastQuote + 1))

let rec pany_string s = 
    match stringEnd s.SourceChunk with 
    | _, Continues   -> s, Empty pany_string, Some s.SourceChunk
    | str, Ends rest -> {s with SourceChunk=rest; Index=0}, Done, Some str
    | _, Partial     -> s, Empty pany_string, Some s.SourceChunk

let exp = pws <|> pbrace1 <|> pws 
         <|> pcitations <|> pws <|> pcolon <|> pws 
         <|> pcitations_list <|> pws
         <|> pcomma <|> pws
         <|> panswer <|> pws <|> pcolon <|> pws <|> pquote
         <!> pany_string <|> pbrace2


let inline d s r = printfn "%A" (s,r)

let append acc o = match o with Some s -> s::acc | _ -> acc

((exp,(State.Empty,Done,[])),source)
||> Seq.scan (fun (p,(s,_,acc)) str ->
    let rec loop p s acc = 
        match  p s with 
        | s, Done, o -> d s Done; pNone, (s,Done, append acc o)
        | s, Fail msg, _ -> fail s msg
        | s, Empty p, o -> d s "Empty"; p,(s,Done, append acc o)
        | s, Cont p, o ->  d s "Cont";  loop p s acc
    loop p {s with SourceChunk = str} acc
)
|> Seq.collect (fun (_,(_,_,os)) -> List.rev os)
|> Seq.iter (printfn "%s")

