open System

let testSuite = 
    [
        """{{ bing.search "SEC regulations and guidelines applicable to special factors" }}"""
        "The weather today is {{weather.getForecast}}"
        "The weather today in {{$city}} is {{weather.getForecast $city}}"
        """The weather today in Schio is {{weather.getForecast "Schio"}}"""
        "{{ bing.search \"\"SEC regulations and guidelines applicable to special factors\"\" }}."
    ]

module TemplateParser =    
    type Block = VarBlock of string | FuncBlock of string*string option

    [<AutoOpen>]
    module internal StateMachine =
        let MAX_LITERAL = 1000
        let eof = Seq.toArray "<end of input>" 
        let inline error x xs = failwithf "%s got %s" x (String(xs |> Seq.truncate 100 |> Seq.toArray))

        let c2s cs = cs |> List.rev |> Seq.toArray |> String

        let toVar = c2s >> VarBlock
        let toFunc1 cs = FuncBlock (c2s cs,None)
        let toFunc2 cs vs = FuncBlock(c2s cs, Some (c2s vs))
        
        let rec start (acc:Block list) = function
            | [] -> acc
            | '{'::rest -> brace1 acc rest 
            | _::rest -> start acc rest
        and brace1 acc = function
            | [] -> error "expected {" eof
            | '{'::rest -> brace2 acc rest
            | x::rest   -> error "expected {" rest
        and brace2 acc = function
            | [] -> error "expecting $ after {{" eof
            | '$'::rest -> beginVar [] acc rest
            | c::rest when Char.IsWhiteSpace c -> brace2 acc rest            
            | c::rest when c <> '}' && c <> '{' -> beginFunc [] acc (c::rest)
            | xs -> error "Expected '$'" xs
        and beginVar vacc acc = function
            | [] -> error "expecting }" eof
            | '}'::rest -> braceEnd1 ((toVar vacc)::acc) rest
            | c::rest when (Char.IsWhiteSpace c) -> braceEnd1 ((toVar vacc)::acc) rest
            | x::rest -> beginVar (x::vacc) acc rest
        and braceEnds acc = function 
            | [] -> error "expecting }}" eof
            | c::rest when Char.IsWhiteSpace c -> braceEnds acc rest 
            | c::rest when c = '}' -> braceEnd1 acc rest
            | c::rest -> error "expected }}" rest
        and braceEnd1 acc = function
            | [] -> error "expecting }" eof
            | '}'::rest -> start acc rest
            | ' '::rest -> braceEnd1 acc rest //can ignore whitespace
            | xs        -> error "expecting }}" xs
        and beginFunc facc acc = function
            | [] -> error "expecting function name" eof
            | c::rest when Char.IsWhiteSpace c -> beginParm [] facc acc rest
            | c::rest when c = '}' -> braceEnd1 ((toFunc1 facc)::acc) rest
            | c::rest -> beginFunc (c::facc) acc rest
        and beginParm pacc facc acc = function
            | [] -> error "expecting function call parameter" eof
            | c::rest when Char.IsWhiteSpace c -> beginParm pacc facc acc rest
            | c::rest when c = '$' -> beginParmVar (c::pacc) facc acc rest
            | c::rest when c = '"' -> beginParmLit [] facc acc rest
            | c::rest -> beginParmVar (c::pacc) facc acc rest
        and beginParmVar pacc facc acc = function
            | [] -> error "expecting parameter name after $" eof
            | c::rest when Char.IsWhiteSpace c -> braceEnds ((toFunc2 facc pacc)::acc) rest
            | c::rest when c = '}' -> braceEnd1 ((toFunc2 facc pacc)::acc) rest
            | c::rest -> beginParmVar (c::pacc) facc acc rest
        and beginParmLit pacc facc acc = function
            | [] -> error """expecting " """ eof
            | c::rest when (List.length pacc > MAX_LITERAL) -> error "max literal size exceeded" rest
            | c::rest when c = '"' -> braceEnds ((toFunc2 facc pacc)::acc) rest
            | c::rest -> beginParmLit (c::pacc) facc acc rest        


    let extractVars templateStr = 
        start [] (templateStr |> Seq.toList)         
        |> List.distinct


let vss = testSuite |> List.map(fun x->x.Replace("\"\"","\"")) |> List.map TemplateParser.extractVars


