namespace FsOpenAI.Server
open System.IO
open System.Threading.Tasks
open FSharp.Control
open FsOpenAI.Client
open FsOpenAI.Client.Interactions
open FsOpenAI.Server.Templates
open Microsoft.AspNetCore.SignalR
open System.Threading.Channels
open System.Text.Json

module Inititalizaiton =

    let initIndexes parms templates dispatch = 
        task {
            try
                match parms.AZURE_SEARCH_ENDPOINTS with 
                | x::_ -> 
                    let! idxs,info = Indexes.fetch parms templates
                    dispatch (Srv_IndexesRefreshed idxs)                 
                    match info with 
                    | Some e -> dispatch (Srv_Info e)
                    | _ -> ()
                | _ ->  ()            
            with ex -> 
                dispatch (Srv_Error ex.Message)
        }

    let initTemplates dispatch = 
        task {
            try
                let! templates = Templates.loadTemplates()
                dispatch (Srv_SetTemplates templates)
            with ex -> 
                dispatch (Srv_Error ex.Message)
        }

    let initSamples dispatch = 
        task {
            try
                let! samples = Samples.loadSamples()
                for s in samples do
                    dispatch (Srv_LoadSamples s)               
            with ex -> 
                dispatch (Srv_Error ex.Message)
        }

    let initConfig dispatch =
        task {
            try
                let path = Path.GetFullPath(Path.Combine(Env.wwwRootPath(),C.APP_CONFIG_PATH))
                let str = File.ReadAllText path
                let appConfig : AppConfig = JsonSerializer.Deserialize(str,Utils.serOptions())
                dispatch (Srv_SetConfig appConfig)
                return Some appConfig
            with ex ->
                printfn "%A" ex
                dispatch (Srv_Info "No application configuration found. Using default config") 
                return None
        }

    let initClient sttngs dispatch =
        task {
            try            
                match! initConfig dispatch with
                | Some cfg -> 
                    do! initIndexes sttngs cfg.IndexGroups dispatch
                    do! initTemplates dispatch
                    do! initSamples dispatch
                | None -> ()
                dispatch (Srv_DoneInit ())
            with ex ->
                dispatch(Srv_Parameters (Env.defaultSettings()))
                dispatch (Srv_Info "No service configuration information found. Initialized with default OpenAI config.") 
        }

module Settings =
    let mutable _cachedSettings = Env.defaultSettings()

    let redactEndpoints (xs:AzureOpenAIEndpoints list) =
        xs 
        |> List.map(fun c -> {c with API_KEY = "Redacted"})

    let redactEndpoint (ep:ApiEndpoint) = {ep with API_KEY="Redacted"}

    let redactSearchEndpoints (xs:ApiEndpoint list) = xs |> List.map redactEndpoint

    let redactKeys (sttngs:ServiceSettings) =
        {sttngs with 
            OPENAI_KEY = None
            AZURE_OPENAI_ENDPOINTS = redactEndpoints sttngs.AZURE_OPENAI_ENDPOINTS
            AZURE_SEARCH_ENDPOINTS = redactSearchEndpoints sttngs.AZURE_SEARCH_ENDPOINTS
            BING_ENDPOINT = sttngs.BING_ENDPOINT |> Option.map redactEndpoint
        }

    let refreshSettings dispatch = 
        task {
            try 
                let! sttngs = Env.getParameters()                
                _cachedSettings <- sttngs
                return (redactKeys sttngs)
            with ex -> 
                Env.logError $"Settings error: {ex.Message}"
                let msg = $"Using default settings (custom settings not configured)"
                dispatch (Srv_Info msg)            
                return (Env.defaultSettings())
        }
    
    let getSettings() = _cachedSettings

    let updateKey sttngs = 
        match sttngs.OPENAI_KEY with 
        | None -> _cachedSettings 
        | _    -> {_cachedSettings with OPENAI_KEY = sttngs.OPENAI_KEY} //use openai from client, if provided

type ServerHub() =
    inherit Hub()

    static member SendMessage(client:ISingleClientProxy, msg:ServerInitiatedMessages) =
        task {
            return! client.SendAsync(ClientHub.fromServer,msg)            
        }

    member this.FromClient(msg:ClientInitiatedMessages) : Task = 
        let cnnId = this.Context.ConnectionId
        let client = this.Clients.Client(cnnId)
        let dispatch msg = ServerHub.SendMessage(client,msg) |> ignore
        task{
            try 
                match msg with 

                | Clnt_Connected _ -> 
                    let! clientSettings = Settings.refreshSettings dispatch
                    dispatch (Srv_Parameters clientSettings)
                    let settings = Settings.getSettings()
                    do! Inititalizaiton.initClient settings dispatch

                | Clnt_ProcessChat (settings,chat) ->
                    let settings = Settings.updateKey settings                       
                    if (Interaction.cBag chat).UseWeb then 
                        WebCompletion.processWebChat settings chat dispatch |> Async.Start
                    else
                        Completions.streamCompleteChat settings chat dispatch |> Async.Start

                | Clnt_RefreshIndexes (settings,initial,templates) ->
                    let settings = Settings.updateKey settings                
                    do! Inititalizaiton.initIndexes settings templates dispatch

                | Clnt_ProcessQA (settings,chat) ->
                    let settings = Settings.updateKey settings
                    let dispatch msg = ServerHub.SendMessage(client,msg) |> ignore
                    QnA.runPlan settings chat dispatch |> Async.Start

                | Clnt_ProcessDocQA (settings,chat) ->
                    let settings = Settings.updateKey settings
                    DocQnA.runPlan settings chat dispatch |> Async.Start

                | Clnt_UploadChunk (fileId,chunk) ->
                    try
                        do! DocQnA.saveChunk (fileId,chunk)
                    with ex ->
                        return raise (HubException(ex.Message))

                | Clnt_ExtractContents (id,fileId) -> 
                    DocQnA.extract (id,fileId) dispatch |> Async.Start

                | Clnt_SearchQuery (settings,ch) -> 
                    let settings = Settings.updateKey settings
                    do! DocQnA.extractQuery settings ch dispatch

            with ex ->
                Env.logError ex.Message

        }

    member this.UploadStream(stream:ChannelReader<byte[]>) : Task =
        task  {
                let mutable i = 0
                do!
                    asyncSeq {
                        let! d  = task {return! stream.ReadAsync()} |> Async.AwaitTask
                        yield d

                    }
                    |> AsyncSeq.iter (fun t -> i <- i + t.Length; printfn "%A" t)
                printfn $"Updloaded {i} bytes"
        }
    
