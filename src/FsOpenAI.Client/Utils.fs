namespace FsOpenAI.Client
open System
open FSharp.Control
open Microsoft.SemanticKernel.SemanticFunctions
open Azure.AI.OpenAI
open Microsoft.SemanticKernel
open Microsoft.Extensions.Logging

module Utils =
    let rng = Random()
    let randSelect (ls:_ list) = ls.[rng.Next(ls.Length)]

    let mutable private id = 0
    let nextId() = Threading.Interlocked.Increment(&id)

    let notEmpty (s:string) = String.IsNullOrWhiteSpace s |> not
    let isEmpty (s:string) = String.IsNullOrWhiteSpace s 

    let isOpen key map = map |> Map.tryFind key |> Option.defaultValue false

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
       if model.Contains("32K",ignoreCase) then 30000
       elif model.Contains("16K",ignoreCase) then 15000
       elif model.Contains("gpt-4",ignoreCase) then 8000
       else 4000

    let getAzureEndpoint (parms:ServiceSettings) =
        if parms.AZURE_OPENAI_ENDPOINTS.IsEmpty then failwith "No Azure OpenAI endpoints configured"
        let endpt = randSelect parms.AZURE_OPENAI_ENDPOINTS
        let url = $"https://{endpt.RESOURCE_GROUP}.openai.azure.com"
        url,endpt.API_KEY

    let getClientFor (parms:ServiceSettings) backend =
            match backend with 
            | AzureOpenAI -> 
                let url,key = getAzureEndpoint parms
                let clr = Azure.AI.OpenAI.OpenAIClient(Uri url,Azure.AzureKeyCredential(key))                        
                clr
            | OpenAI  ->     
                let key = match parms.OPENAI_KEY with Some key when notEmpty key -> key | _ -> failwith "OpenAI Key not set"
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

    let baseKernel (parms:ServiceSettings) (ch:Interaction) = 
        let chParms = ch.Parameters
        let chatModel = chParms.ChatModel
        let embModel = chParms.EmbeddingsModel
        let compModel = chParms.CompletionsModel
        match ch.Parameters.Backend with 
        | AzureOpenAI ->
            let uri,key = getAzureEndpoint parms
            KernelBuilder()                                        
                .WithLogger(logger)
                .WithAzureChatCompletionService(chatModel,uri,apiKey=key)
                .WithAzureTextEmbeddingGenerationService(embModel,uri,apiKey=key)
                //.WithAzureTextCompletionService(compModel,uri,apiKey=key)                                        
        | OpenAI ->
            let key = match parms.OPENAI_KEY with Some k -> k | None -> raise (NoOpenAIKey "No OpenAI Key found")
            KernelBuilder()
                .WithLogger(logger)
                .WithOpenAIChatCompletionService(chatModel,key)
                .WithOpenAITextEmbeddingGenerationService(embModel,key)
                //.WithOpenAITextCompletionService(compModel,key)

    let toCompletionsConfig (p:InteractionParameters) =
       PromptTemplateConfig.CompletionConfig(
        Temperature=p.Temperature,
        MaxTokens=p.MaxTokens,
        FrequencyPenalty = p.FrequencyPenalty,
        PresencePenalty = p.PresencePenalty)

module C =
    let LS_OPENAI_KEY = "LS_OPENAI_KEY"
    let MAIN_SETTINGS = "MAIN_SETTINGS"
    let CHAT_DOCS chatId = $"{chatId}_docs"
