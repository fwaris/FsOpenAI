namespace FsOpenAI.GenAI
open System
open System.Text.Json
open FSharp.Control
open System.Threading.Channels
open FsOpenAI.Shared
open FSharp.CosmosDb

module Version =
    let version = "0.1.0" //serialize format version change this when conversion to new format is required

type SessionMsg = {
    Role : string
    Content : string
}

type ChatSession = {
    [<Id>]
    id: string
    [<PartitionKey>]
    UserId : string
    AppId : string
    Timestamp : DateTime
    Version : string
    Interaction : Interaction
}

type SessionOp =
    | Upsert of ChatSession
    | Delete of InvocationContext*string
    | ClearAll of InvocationContext

[<RequireQualifiedAccess>]
module Sessions =
    let BUFFER_SIZE = 1000
    let BUFFER_WAIT = 10000
    let MAX_SESSIONS = 15

    let mutable private _cnctnInfo = lazy None

    let init (ccstr,database,container) =
        match Connection.tryCreate<ChatSession>(ccstr,database,container) with
        | Some x -> _cnctnInfo <- lazy(Some x)
        | None -> ()

    let getConnectionFromConfig() =
        try
            Env.appConfig.Value
            |> Option.bind(fun x -> Env.logInfo $"{x.DatabaseName},{x.SessionTableName}"; x.SessionTableName |> Option.map(fun t -> x.DatabaseName,t))
            |> Option.bind(fun (database,container) ->
                Settings.getSettings().Value.LOG_CONN_STR
                |> Option.map(fun cstr -> Env.logInfo $"{Utils.shorten 30 cstr}";cstr,database,container))
        with ex ->
            Env.logException (ex,"Monitoring.getConnectionFromConfig")
            None

    let ensureConnection() =
        match _cnctnInfo.Value with
        | Some _ -> ()
        | None ->
            match getConnectionFromConfig() with
            | Some (cstr,db,cntnr) -> init(cstr,db,cntnr)
            | None -> ()


    type SREf = {[<Id>] id:string; [<PartitionKey>] UserId:string; Timestamp:DateTime}

    let private saveSessionsForUser ((userId:string,appId:string), chatSessions:ChatSession[]) =
        async {
            match _cnctnInfo.Value with
            | Some c ->
                try
                    let db =
                        Cosmos.fromConnectionString c.ConnectionString
                        |> Cosmos.database c.DatabaseName
                        |> Cosmos.container c.ContainerName
                    do!
                        db
                        |> Cosmos.upsertMany (Array.toList chatSessions)
                        |> Cosmos.execAsync
                        |> AsyncSeq.iter (fun _ -> ())
                    let ls =
                        db
                        |> Cosmos.query<SREf>(sprintf $"SELECT c.id, c.UserId, c.Timestamp FROM c WHERE c.UserId = @UserId and c.AppId = @AppId" )
                        |> Cosmos.parameters ["@UserId", box userId; "@AppId", box appId]
                        |> Cosmos.execAsync
                        |> AsyncSeq.toBlockingSeq
                        |> Seq.toList
                    let sessions = ls |> List.sortBy(fun x -> x.Timestamp)
                    if sessions.Length > MAX_SESSIONS then
                        let dropSessions = sessions |> List.take (MAX_SESSIONS - sessions.Length)
                        do!
                            dropSessions
                            |> AsyncSeq.ofSeq
                            |> AsyncSeq.iterAsync (fun c ->
                                db
                                |> Cosmos.deleteItem<ChatSession> c.id c.UserId
                                |> Cosmos.execAsync
                                |> AsyncSeq.iterAsync Async.Ignore)
                with ex ->
                    Env.logException (ex,"Sessions.saveSessionsForUser: ")
            | None -> ()
        }

    let private saveSessions (chatSessions:ChatSession[]) =
        async {
            match _cnctnInfo.Value with
            | Some _ ->
                try
                    do!
                        chatSessions
                        |> Array.groupBy(fun x -> x.UserId,x.AppId)
                        |> Array.map saveSessionsForUser
                        |> Async.Parallel
                        |> Async.Ignore
                with ex ->
                    Env.logException (ex,"Sessions.saveSessions: ")
            | None -> ()
        }

    let private channel = Channel.CreateBounded<SessionOp>(BoundedChannelOptions(BUFFER_SIZE,FullMode = BoundedChannelFullMode.DropOldest))

    let private delete (invCtx:InvocationContext,id:string) =
        async {
            match _cnctnInfo.Value with
            | Some c ->
                try
                    let user = invCtx.User |> Option.defaultValue null
                    let db =
                        Cosmos.fromConnectionString c.ConnectionString
                        |> Cosmos.database c.DatabaseName
                        |> Cosmos.container c.ContainerName
                    do!
                        db
                        |> Cosmos.deleteItem<ChatSession> id user
                        |> Cosmos.execAsync
                        |> AsyncSeq.iter (fun _ -> ())
                with ex ->
                    Env.logException (ex,"Sessions.delete: ")
            | None -> ()
        }

    let private clearAll (invCtx:InvocationContext) =
        async {
            match _cnctnInfo.Value with
            | Some c ->
                try
                    let db =
                        Cosmos.fromConnectionString c.ConnectionString
                        |> Cosmos.database c.DatabaseName
                        |> Cosmos.container c.ContainerName
                    let userId = invCtx.User |> Option.defaultValue C.UNAUTHENTICATED
                    let appId = invCtx.AppId |> Option.defaultValue C.DFLT_APP_ID
                    let dropSessions =
                        db
                        |> Cosmos.query<SREf>(sprintf $"SELECT c.id, c.UserId, c.Timestamp FROM c WHERE c.UserId = @UserId and c.AppId = @AppId" )
                        |> Cosmos.parameters ["@UserId", box userId; "@AppId", box appId]
                        |> Cosmos.execAsync
                        |> AsyncSeq.toBlockingSeq
                        |> Seq.toList
                    do!
                        dropSessions
                        |> AsyncSeq.ofSeq
                        |> AsyncSeq.iterAsync (fun c ->
                            async {
                                try
                                    do!
                                        db
                                        |> Cosmos.deleteItem<ChatSession> c.id c.UserId
                                        |> Cosmos.execAsync
                                        |> AsyncSeq.iter (fun x -> ())
                                with ex ->
                                    Env.logError ex.Message
                            })
                with ex ->
                    Env.logException (ex,"Sessions.delete: ")
            | None -> ()
        }

    let private applyOps (ops:SessionOp[]) =
        async {
            let gops = ops |> Array.groupBy (function | Upsert _ -> 0 | Delete _ -> 1 | ClearAll _ -> 2)
            for (k,ops) in gops do
                match k with
                | 0 ->
                    do!
                        ops
                        |> Array.choose (fun op -> match op with | Upsert session -> Some session | _ -> None)
                        |> saveSessions
                | 1 ->
                    do!
                        ops
                        |> AsyncSeq.ofSeq
                        |> AsyncSeq.choose (fun op -> match op with | Delete (invCtx,id) -> Some (invCtx,id) | _ -> None)
                        |> AsyncSeq.iterAsync delete

                | 2 ->
                    do!
                        ops
                        |> AsyncSeq.ofSeq
                        |> AsyncSeq.choose (fun ops -> match ops with | ClearAll invCtx -> Some invCtx | _ -> None)
                        |> AsyncSeq.iterAsync clearAll

                | x -> failwith $"operaion type {x} not handled in applyOps"
        }

    //background loop to read channel and write diagnostics entry to backend
    let private consumerLoop =
        asyncSeq {
            while true do
                let! data = channel.Reader.ReadAsync().AsTask() |> Async.AwaitTask
                yield data
        }
        |> AsyncSeq.bufferByCountAndTime 10 BUFFER_WAIT
        |> AsyncSeq.iterAsync applyOps
        |> Async.Start

    let queueOp session = channel.Writer.WriteAsync session |> ignore

    let toSession (invCtx:InvocationContext) (ch:Interaction) =
        let userId = invCtx.User  |> Option.defaultValue C.UNAUTHENTICATED
        let appId = invCtx.AppId |> Option.defaultValue C.DFLT_APP_ID
        let timestamp = DateTime.UtcNow
        {
            id          = ch.Id
            UserId      = userId
            AppId       = appId
            Timestamp   = timestamp
            Version     = Version.version
            Interaction = ch
        }

    let tryConvert (str:string,ch:JsonDocument) =
        let ver = ch.RootElement.GetProperty("Version").GetString()
        if ver = Version.version then
            let sess = System.Text.Json.JsonSerializer.Deserialize<ChatSession>(str,Utils.serOptions())
            //let sess = Newtonsoft.Json.JsonConvert.DeserializeObject<ChatSession>(str)
            Some sess.Interaction
        else
            //TODO: convert to new verison from old serialized format
            None

    let loadSessions (invCtx:InvocationContext) =
        let userId = invCtx.User  |> Option.defaultValue C.UNAUTHENTICATED
        let appId = invCtx.AppId |> Option.defaultValue C.DFLT_APP_ID
        match _cnctnInfo.Value with
        | Some c ->
            let db =
                Cosmos.fromConnectionString c.ConnectionString
                |> Cosmos.database c.DatabaseName
                |> Cosmos.container c.ContainerName
            db
            |> Cosmos.query<SREf>(sprintf $"SELECT c.id, c.UserId, c.Timestamp FROM c WHERE c.UserId = @UserId and c.AppId = @AppId ORDER BY c.Timestamp DESC" )
            |> Cosmos.parameters ["@UserId", box userId; "@AppId", box appId]
            |> Cosmos.execAsync
            |> AsyncSeq.collect (fun sref ->
                db
                |> Cosmos.read sref.id sref.UserId
                |> Cosmos.execAsync)
            |> AsyncSeq.map(fun j ->
                let doc = j.ToString()
                doc,System.Text.Json.JsonDocument.Parse(doc))
            |> AsyncSeq.choose tryConvert

        | None -> AsyncSeq.empty
