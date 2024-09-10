namespace FsOpenAI.Server
open System.Threading.Tasks
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open FsOpenAI.Server.Templates
open FsOpenAI.GenAI
open Microsoft.AspNetCore.SignalR
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives

module Inititalizaiton =

    let initIndexes parms templates metaIndex dispatch =
        task {
            try
                match parms.AZURE_SEARCH_ENDPOINTS with
                | x::_ ->
                    let! idxTrs,info = Indexes.fetch parms templates metaIndex
                    dispatch (Srv_IndexesRefreshed idxTrs)
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

    let initClient sttngs dispatch =
        task {
            try
                match Env.appConfig.Value with
                | Some cfg ->
                    dispatch (Srv_SetConfig cfg)
                    match cfg.MetaIndex with
                    | Some metaIndex -> do! initIndexes sttngs cfg.IndexGroups metaIndex dispatch
                    | None -> ()
                    do! initTemplates dispatch
                    do! initSamples dispatch
                | None ->
                    dispatch (Srv_Info "No application configuration found. Using default config")
                dispatch (Srv_DoneInit ())
            with ex ->
                dispatch(Srv_Parameters (Env.defaultSettings()))
                dispatch (Srv_Info "No service configuration information found. Initialized with default OpenAI config.")
        }

///For websocket connection, token is passed as query parameter. This handler copies it to header
/// so that it can be used for authorization
type TokenHandler(next:RequestDelegate) =
    member this.Invoke(context:HttpContext) =
        match context.Request.Query.TryGetValue("access_token") with 
        | true, tokens -> 
            if tokens.Count > 0 then 
                let token = tokens.[0]
                let hdr = StringValues("Bearer " + token)
                context.Request.Headers.Add("Authorization", hdr)
        | _ -> ()
        next.Invoke(context)    
    
type ServerHub() =
    inherit Hub()

    static member SendMessage(client:ISingleClientProxy, msg:ServerInitiatedMessages) =
        task {
            return! client.SendAsync(C.ClientHub.fromServer,msg)
        }

    //allows unauthenticated users access to initial settings
    member this.FromClientUnAuth(msg:ClientInitiatedMessages) : Task =
        let cnnId = this.Context.ConnectionId
        let client = this.Clients.Client(cnnId)
        let dispatch msg = ServerHub.SendMessage(client,msg) |> ignore
        task {
            try
                match msg with

                | Clnt_Connected _ ->
                    do! Settings.refreshSettings dispatch  //we refresh keys at client connect time 
                    try Monitoring.ensureConnection() with ex -> dispatch (Srv_Error ex.Message)
                    try Sessions.ensureConnection() with ex ->  dispatch (Srv_Error ex.Message)
                    do! Inititalizaiton.initClient (Settings.getSettings().Value) dispatch
                | _ -> dispatch (Srv_Error "Unauthorized access")
            with ex -> 
                Env.logError ex.Message
        }

    member private this.ProcessClientMessage(msg:ClientInitiatedMessages) : Task = 
        let cnnId = this.Context.ConnectionId
        let client = this.Clients.Client(cnnId)
        let dispatch msg = ServerHub.SendMessage(client,msg) |> ignore
        task{
            try
                match msg with

                | Clnt_Connected _ ->
                    () //do nothing as we are already connected

                | Clnt_Run_Plain (settings,invCtx,chat) ->
                    let settings = Settings.updateKey settings
                    if (Interaction.cBag chat).UseWeb then
                        WebCompletion.processWebChat settings invCtx chat dispatch |> Async.Start
                    else
                        Completions.streamCompleteChat settings invCtx chat dispatch None |> Async.Start

                | Clnt_RefreshIndexes (settings,initial,templates,metaIndex) ->
                    let settings = Settings.updateKey settings
                    do! Inititalizaiton.initIndexes settings templates metaIndex dispatch

                | Clnt_Run_IndexQnA (settings,invCtx,chat) ->
                    let settings = Settings.updateKey settings
                    IndexQnA.runPlan settings invCtx chat dispatch |> Async.Start

                | Clnt_Run_QnADoc (settings,invCtx,chat) ->
                    let settings = Settings.updateKey settings
                    DocQnA.runPlan settings invCtx chat dispatch |> Async.Start

                | Clnt_Run_IndexQnADoc (settings,invCtx,chat) ->
                    let settings = Settings.updateKey settings
                    DocQnA.runPlan settings invCtx chat dispatch |> Async.Start

                | Clnt_Run_EvalCode (settings,invCtx,chat,evalParms) ->
                    let settings = Settings.updateKey settings
                    FsOpenAI.CodeEvaluator.CodeEval.run settings invCtx chat evalParms dispatch |> Async.Start

                | Clnt_UploadChunk (fileId,chunk) ->
                    try
                        do! DocQnA.saveChunk (fileId,chunk)
                    with ex ->
                        return raise (HubException(ex.Message))

                | Clnt_Ia_Doc_Extract ((stngs,invCtx,bkend),(id,fileId,docType)) ->
                    let settings = Settings.updateKey stngs
                    let parms = (settings,invCtx,bkend)
                    DocQnA.extract parms (id,fileId,docType) dispatch |> Async.Start

                | Clnt_Ia_Session_Save (invCtx,ch) ->
                    let session = Sessions.toSession invCtx ch
                    do Sessions.queueOp (Upsert session)

                | Clnt_Ia_Session_Delete (invCtx,id) ->
                    do Sessions.queueOp (Delete (invCtx,id))

                | Clnt_Ia_Feedback_Submit (invCtx,fb) ->
                    let fbe = 
                        {
                            UserId = invCtx.User |> Option.defaultValue C.UNAUTHENTICATED
                            LogId=fb.LogId
                            Feedback = {ThumbsUpDn = fb.ThumbsUpDn; Comment = fb.Comment;}
                        }
                    Monitoring.write (Feedback fbe)
                    dispatch (Srv_Info "Feedback submitted. Thanks!")

                | Clnt_Ia_Session_LoadAll invCtx ->
                    do!
                        async {
                            let comp =
                                Sessions.loadSessions invCtx
                                |> AsyncSeq.iter (fun ch -> dispatch (Srv_Ia_Session_Loaded ch))
                            match! Async.Catch comp with
                            | Choice1Of2 _ -> ()
                            | Choice2Of2 ex ->
                                Env.logException(ex,"Clnt_LoadChatSessions: ")
                                dispatch (Srv_Info "Due to format change, saved sessions cannot be loaded. They will be cleared. Please create new session from top right menu")
                                do Sessions.queueOp (ClearAll invCtx) //clear all saved sessions to avoid future errors
                            dispatch Srv_Ia_Session_DoneLoading
                        }

                | Clnt_Ia_Session_ClearAll invCtx ->
                    do Sessions.queueOp (ClearAll invCtx)

            with ex ->
                Env.logError ex.Message
        }

    //[<Authorize>] 
    member this.FromClient(msg:ClientInitiatedMessages) : Task =
        let cfg =   Env.appConfig.Value
        match cfg with
        | Some v when v.RequireLogin ->
            let isAuthenticated = this.Context.User.Identity.IsAuthenticated
            let hasRole = 
                match v.Roles with
                | [] -> true  // app does not require any role
                | xs -> xs |> List.exists (fun r -> this.Context.User.IsInRole(r))
            if isAuthenticated && hasRole then
                this.ProcessClientMessage msg
            else
                task {return raise (HubException("Unauthorized access"))}
        | _ -> 
            this.ProcessClientMessage msg
                
