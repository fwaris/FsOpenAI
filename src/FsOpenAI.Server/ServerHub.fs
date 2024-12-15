namespace FsOpenAI.Server
open System
open System.Threading.Tasks
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open FsOpenAI.Server.Templates
open FsOpenAI.GenAI
open Microsoft.AspNetCore.SignalR
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.DependencyInjection

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
                Env.logException(ex,"initIndexes")
                dispatch (Srv_Error "Unable to fetch index data")
        }

    let initTemplates dispatch =
        task {
            try
                let! templates = Templates.loadTemplates()
                dispatch (Srv_SetTemplates templates)
            with ex ->
                Env.logException(ex,"initTemplates")
                dispatch (Srv_Error "Unable to load templates")
        }

    let initSamples dispatch =
        task {
            try
                let! samples = Samples.loadSamples()
                for s in samples do
                    dispatch (Srv_LoadSamples s)
            with ex ->
                Env.logException(ex,"initSamples")
                dispatch (Srv_Error "Unable to load samples")
        }

    let initClient dispatch =
        let cfgMissingMsg = "No application configuration found. Using default config"
        task {
            try
                match Env.appConfig.Value with
                | Some cfg ->
                    dispatch (Srv_SetConfig cfg)
                    do! Settings.refreshSettings dispatch
                    let sttngs = Settings.getSettings().Value
                    match cfg.MetaIndex with
                    | Some metaIndex -> do! initIndexes sttngs cfg.IndexGroups metaIndex dispatch
                    | None -> ()
                    do! initTemplates dispatch
                    do! initSamples dispatch
                | None ->
                    dispatch (Srv_Info cfgMissingMsg)
            with ex ->
                dispatch(Srv_Parameters (Env.defaultSettings()))
                dispatch (Srv_Info cfgMissingMsg)
        }

///For websocket connection, token is passed as query parameter. This handler copies it to header
/// so that it can be used for authorization
/// Note: HTTP headers and query string are already parsed by middleware
/// before control reaches here
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
        
#if UNAUTHENTICATED
#else    
[<Authorize>]
#endif
type ServerHub() =
    inherit Hub()

    let getUser (ctx:HubCallerContext) =
        match ctx.User with 
        | null -> C.UNAUTHENTICATED
        | u -> FsOpenAI.Client.Auth.getEmail u
    
    let updateCtx invCtx ctx = {invCtx with User = Some (getUser ctx)}
    
    ///Force disconnects any client, if token in the client context is expired or is about to expire
    ///Note: Middlware (gateways etc.) may disconnect web socket connections long before token expiry
    ///in which case invoking this function is mute
    let checkTokenExpiry (client:ISingleClientProxy, ctx:HubCallerContext) = 
        task {
            let hctx = ctx.GetHttpContext()                        
            let! result =  hctx.AuthenticateAsync()
            match result with 
            | null -> ()
            | result -> 
                if result.Ticket.Properties.ExpiresUtc.HasValue then 
                    if result.Ticket.Properties.ExpiresUtc.Value.UtcDateTime < DateTime.UtcNow.AddMinutes(5.0) then                         
                        do! client.SendCoreAsync("Disconnect",[||])
                        ctx.Abort()
        }
        |> ignore
        
    static member SendMessage(client:ISingleClientProxy, msg:ServerInitiatedMessages) =
        task {
            return! client.SendAsync(C.ClientHub.fromServer,msg)
        }

    member private this.ProcessClientMessage(msg:ClientInitiatedMessages) : Task = 
        let cnnId = this.Context.ConnectionId
        let client = this.Clients.Client(cnnId)
        let dispatch msg = ServerHub.SendMessage(client,msg) |> ignore
        task{
            try
                match msg with

                | Clnt_Connected _ ->
                    do! Inititalizaiton.initClient dispatch
                    dispatch (Srv_DoneInit ())

                | Clnt_Run_Plain (settings,invCtx,chat) ->
                    let invCtx = updateCtx invCtx this.Context
                    let settings = Settings.updateKey settings
                    if (Interaction.cBag chat).UseWeb then
                        WebCompletion.processWebChat settings invCtx chat dispatch |> Async.Start
                    else
                        Completions.checkStreamCompleteChat settings invCtx chat dispatch None false |> Async.Start

                | Clnt_RefreshIndexes (settings,initial,templates,metaIndex) ->
                    let settings = Settings.updateKey settings
                    do! Inititalizaiton.initIndexes settings templates metaIndex dispatch

                | Clnt_Run_IndexQnA (settings,invCtx,chat) ->
                    let invCtx = updateCtx invCtx this.Context
                    let settings = Settings.updateKey settings
                    IndexQnA.runPlan settings invCtx chat dispatch |> Async.Start

                | Clnt_Run_QnADoc (settings,invCtx,chat) ->
                    let invCtx = updateCtx invCtx this.Context
                    let settings = Settings.updateKey settings
                    DocQnA.runPlan settings invCtx chat dispatch |> Async.Start

                | Clnt_Run_IndexQnADoc (settings,invCtx,chat) ->
                    let invCtx = updateCtx invCtx this.Context
                    let settings = Settings.updateKey settings
                    DocQnA.runPlan settings invCtx chat dispatch |> Async.Start

                | Clnt_Run_EvalCode (settings,invCtx,chat,evalParms) ->
#if UNAUTHENTICATED
                    //experimental LLM code gen & evaluator - not for production deployment                
                    let invCtx = updateCtx invCtx this.Context
                    FsOpenAI.CodeEvaluator.CodeEval.run settings invCtx chat evalParms dispatch |> Async.Start                
#else
                    dispatch (Srv_Ia_Done (chat.Id, Some "Code evaluation disabled" ))
#endif

                | Clnt_UploadChunk (fileId,chunk) ->
                    try
                        do! DocQnA.saveChunk (fileId,chunk)
                    with ex ->
                        Env.logException(ex,"upload chunk")
                        return raise (HubException("upload"))

                | Clnt_Ia_Doc_Extract ((stngs,invCtx,bkend),(id,fileId,docType)) ->
                    let invCtx = updateCtx invCtx this.Context
                    let settings = Settings.updateKey stngs
                    let parms = (settings,invCtx,bkend)
                    DocQnA.extract parms (id,fileId,docType) dispatch |> Async.Start

                | Clnt_Ia_Session_Save (invCtx,ch) ->
                    let invCtx = updateCtx invCtx this.Context
                    let session = Sessions.toSession invCtx ch
                    do Sessions.queueOp (Upsert session)

                | Clnt_Ia_Session_Delete (invCtx,id) ->
                    let invCtx = updateCtx invCtx this.Context
                    do Sessions.queueOp (Delete (invCtx,id))

                | Clnt_Ia_Feedback_Submit (invCtx,fb) ->                    
                    let invCtx = updateCtx invCtx this.Context
                    let comment = fb.Comment |> Option.map (fun c -> Utils.shorten C.MAX_COMMENT_LENGTH c)
                    let fbe = 
                        {
                            UserId = invCtx.User |> Option.defaultValue C.UNAUTHENTICATED
                            LogId=fb.LogId
                            Feedback = {ThumbsUpDn = fb.ThumbsUpDn; Comment = comment;}
                        }
                    Monitoring.write (Feedback fbe)
                    dispatch (Srv_Info "Feedback submitted. Thanks!")

                | Clnt_Ia_Session_LoadAll invCtx ->
                    let invCtx = updateCtx invCtx this.Context
                    do!
                        async {
                            let comp =
                                Sessions.loadSessions invCtx
                                |> AsyncSeq.iter (fun ch -> dispatch (Srv_Ia_Session_Loaded ch))
                            match! Async.Catch comp with
                            | Choice1Of2 _ -> ()
                            | Choice2Of2 ex ->
                                Env.logException(ex,"Clnt_LoadChatSessions: ")
                                dispatch (Srv_Info "Due to format change, saved chat sessions cannot be loaded. They will be cleared. Please create new chat")
                                do Sessions.queueOp (ClearAll invCtx) //clear all saved sessions to avoid future errors
                            dispatch Srv_Ia_Session_DoneLoading
                        }

                | Clnt_Ia_Session_ClearAll invCtx ->
                    let invCtx = updateCtx invCtx this.Context
                    do Sessions.queueOp (ClearAll invCtx)

            with ex ->
                Env.logException(ex, nameof this.ProcessClientMessage)
            
            //checkExpiry(client,this.Context)
        }

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
                
