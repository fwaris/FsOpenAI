module FsOpenAI.GenAI.SKernel
open Microsoft.SemanticKernel
open Microsoft.Extensions.Logging
open Microsoft.SemanticKernel.Connectors.OpenAI
open Microsoft.Extensions.DependencyInjection
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.GenAI.Endpoints
open FsOpenAI.GenAI.ChatUtils

[<RequireQualifiedAccess>]
module SKernel =
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

    let promptSettings (parms:ServiceSettings) (ch:Interaction) =
        new OpenAIPromptExecutionSettings(
            MaxTokens = ch.Parameters.MaxTokens,
            Temperature = (ChatUtils.temperature ch.Parameters.Mode |> float),
            TopP = 1)

    let baseKernel (parms:ServiceSettings) (modelRefs:ModelRef list) (ch:Interaction) =
        let chatModel = modelRefs.Head.Model
        let builder = Kernel.CreateBuilder()
        builder.Services.AddLogging(fun c -> c.AddConsole().SetMinimumLevel(LogLevel.Information) |>ignore) |> ignore
        let ep = Endpoints.endpoint parms ch.Parameters.Backend
        match ch.Parameters.Backend.Name with
        | KnownBackends.AzureOpenAI ->
            builder.AddAzureOpenAIChatCompletion(deploymentName = chatModel,endpoint = ep.ENDPOINT, apiKey = ep.API_KEY)
        | KnownBackends.OpenAI ->
            builder.AddOpenAIChatCompletion(chatModel,apiKey = ep.API_KEY)
        | x -> 
            builder.AddOpenAIChatCompletion(chatModel,endpoint=System.Uri ep.ENDPOINT,apiKey = ep.API_KEY)

    let kernelArgsFrom parms ch (args:(string*string) seq) =
        let sttngs = promptSettings parms ch
        let kargs = KernelArguments(sttngs)
        for (k,v) in args do
            kargs.Add(k,v)
        kargs

    let kernelArgsDefault (args:(string*string) seq) =
        let sttngs = new OpenAIPromptExecutionSettings(MaxTokens = 150, TopP = 1)
        let kargs = KernelArguments(sttngs)
        for (k,v) in args do
            kargs.Add(k,v)
        kargs

    let kernelArgs (args:(string*string) seq) (overrides:OpenAIPromptExecutionSettings->unit) =
        let args = kernelArgsDefault args
        args.ExecutionSettings
        |> Seq.iter(fun kv ->
            let sttngs = (kv.Value :?> OpenAIPromptExecutionSettings)
            overrides sttngs)
        args

    let renderPrompt (prompt:string) (args:KernelArguments) =
        task {
            let k = Kernel.CreateBuilder().Build()
            let fac = KernelPromptTemplateFactory()
            let cfg = PromptTemplateConfig(template = prompt)
            let pt = fac.Create(cfg)
            let! rslt = pt.RenderAsync(k,args) |> Async.AwaitTask
            return rslt
        }

