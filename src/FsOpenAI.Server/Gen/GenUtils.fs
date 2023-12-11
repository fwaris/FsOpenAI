namespace FsOpenAI.Client
open System
open FSharp.Control
open Microsoft.SemanticKernel
open Azure.AI.OpenAI
open Microsoft.Extensions.Logging
open Microsoft.DeepDev


module GenUtils =
    open Microsoft.SemanticKernel.Memory
    let rng = Random()
    let randSelect (ls:_ list) = ls.[rng.Next(ls.Length)]

    let tokenSize (s:string) =         
        let tokenizer = TokenizerBuilder.CreateByModelNameAsync("gpt-4").GetAwaiter().GetResult();
        let tokens = tokenizer.Encode(s, new System.Collections.Generic.HashSet<string>());
        float tokens.Count

    let asAsyncSeq<'t> (xs:System.Collections.Generic.IAsyncEnumerable<'t>) = 
        asyncSeq {
            let mutable hs = false
            let xs = xs.GetAsyncEnumerator()
            let! hasNext = task{return! xs.MoveNextAsync()} |> Async.AwaitTask
            hs <- hasNext
            while hs do
                yield xs.Current
                let! hasNext = task{return! xs.MoveNextAsync()} |> Async.AwaitTask
                hs <- hasNext
            xs.DisposeAsync() |> ignore
        }

    exception NoOpenAIKey of string

    let ignoreCase = StringComparison.InvariantCultureIgnoreCase

    //token limits
    let safeTokenLimit (model:string) = 
       if model.Contains("32K",ignoreCase) then 30000.
       elif model.Contains("16K",ignoreCase) then 15000.
       elif model.Contains("gpt-4",ignoreCase) then 8000.
       else 4000.

    let getAzureEndpoint (parms:ServiceSettings) =
        if parms.AZURE_OPENAI_ENDPOINTS.IsEmpty then failwith "No Azure OpenAI endpoints configured"
        let endpt = randSelect parms.AZURE_OPENAI_ENDPOINTS
        let rg = endpt.RESOURCE_GROUP
        let url = $"https://{rg}.openai.azure.com"
        rg,url,endpt.API_KEY

    let getClientFor (parms:ServiceSettings) backend =
            match backend with 
            | AzureOpenAI -> 
                let rg,url,key = getAzureEndpoint parms
                let clr = Azure.AI.OpenAI.OpenAIClient(Uri url,Azure.AzureKeyCredential(key))                        
                clr
            | OpenAI  ->     
                let key = match parms.OPENAI_KEY with Some key when Utils.notEmpty key -> key | _ -> failwith "OpenAI Key not set"
                let opts = new OpenAIClientOptions(version=OpenAIClientOptions.ServiceVersion.V2023_05_15)
                Azure.AI.OpenAI.OpenAIClient(parms.OPENAI_KEY.Value,opts)                

    let getClient (parms:ServiceSettings) (ch:Interaction) = getClientFor parms ch.Parameters.Backend

    let logger = 
        {new ILogger with
             member this.BeginScope(state) = raise (System.NotImplementedException())
             member this.IsEnabled(logLevel) = true
             member this.Log(logLevel, eventId, state, ``exception``, formatter) = 
                let msg = formatter.Invoke(state,``exception``)
                printfn "Kernel: %s" msg
        }

    let loggerFactory = 
        {new ILoggerFactory with
             member this.AddProvider(provider) = ()
             member this.CreateLogger(categoryName) = logger
             member this.Dispose() = ()
        }

    let baseKernel (parms:ServiceSettings) (ch:Interaction) = 
        let chParms = ch.Parameters
        let chatModel = chParms.ChatModel
        let embModel = chParms.EmbeddingsModel
        let compModel = chParms.CompletionsModel
        match ch.Parameters.Backend with 
        | AzureOpenAI ->
            let rg,uri,key = getAzureEndpoint parms
            KernelBuilder().AddAzureOpenAIChatCompletion(chatModel,chatModel,uri,key)                
        | OpenAI ->
            let key = match parms.OPENAI_KEY with Some k -> k | None -> raise (NoOpenAIKey "No OpenAI Key found")
            KernelBuilder().AddOpenAIChatCompletion(chatModel,key)

    let searchResults parms ch maxDocs query (cogMems:ISemanticTextMemory seq) =
        cogMems
        |> AsyncSeq.ofSeq
        |> AsyncSeq.collect(fun cogMem ->             
            cogMem.SearchAsync("",query,maxDocs) |> AsyncSeq.ofAsyncEnum)
        |> AsyncSeq.toBlockingSeq
        |> Seq.toList

module TemplateParser =    
    type Block = VarBlock of string | FuncBlock of string*string option

    [<AutoOpen>]
    module internal StateMachine =
        let MAX_LITERAL = 3000
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
