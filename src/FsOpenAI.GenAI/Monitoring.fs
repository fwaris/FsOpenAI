namespace FsOpenAI.GenAI
open System
open Azure
open FSharp.Control
open Azure.AI.OpenAI
open System.Threading.Channels
open FsOpenAI.Shared
open FSharp.CosmosDb
open Microsoft.Azure.Cosmos

type ChatLogMsg = {
    Role : string
    Content : string
}

type ChatLog = {
    SystemMessge: string
    Messages : ChatLogMsg list
    Temperature : float
    MaxTokens : int
}

type PromptLog =
    | Embedding of string
    | Chat of ChatLog

type MFeedback = {
    ThumbsUpDn  : int
    Comment     : string option
}


type FeedbackEntry = {
    LogId : string
    UserId : string
    Feedback : MFeedback
}

type MIndexRef = {
    Backend: string
    Name: string
}

type DiagEntry = {
    [<Id>]
    id : string
    AppId : string
    [<PartitionKey>]
    UserId : string
    Prompt : PromptLog
    Feedback : MFeedback option
    Question : string
    Response : string
    InputTokens : int
    OutputTokens : int
    Error : string
    Backend : string
    Resource : string
    Model : string
    IndexRefs : MIndexRef list
    Timestamp : DateTime
}

type LogEntry = Diag of DiagEntry | Feedback of FeedbackEntry

[<AutoOpen>]
module Monitoring =
    let BUFFER_SIZE = 1000
    let BUFFER_WAIT = 10000

    let mutable private _cnctnInfo = lazy None

    let init (ccstr,database,container) =
        match Connection.tryCreate(ccstr,database,container) with
        | Some x -> _cnctnInfo <- lazy(Some x)
        | None -> ()

    let getConnectionFromConfig() =
        try
            Env.appConfig.Value
            |> Option.bind(fun x -> Env.logInfo $"{x.DatabaseName},{x.DiagTableName}"; x.DiagTableName |> Option.map(fun t -> x.DatabaseName,t))
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

    let private writeDiagAsync (diagEntries:DiagEntry[]) =
        async {
            match _cnctnInfo.Value with
            | Some c  ->
                try
                    do!
                        Cosmos.fromConnectionString c.ConnectionString
                            |> Cosmos.database c.DatabaseName
                            |> Cosmos.container c.ContainerName
                            |> Cosmos.upsertMany (Array.toList diagEntries)
                            |> Cosmos.execAsync
                            |> AsyncSeq.iter (fun _ -> ())
                with ex ->
                    Env.logException (ex,"writeLog")
            | None -> ()
        }

    let private updateDiagEntry (fb:MFeedback) (de:DiagEntry) =
        {de with Feedback = Some fb}

    let private updateWithFeedbackAsync (fbEntries:FeedbackEntry[]) =
        async {
            match _cnctnInfo.Value with
            | Some c ->
                try
                    let db =
                        Cosmos.fromConnectionString c.ConnectionString
                            |> Cosmos.database c.DatabaseName
                            |> Cosmos.container c.ContainerName

                    do!
                        fbEntries
                        |> AsyncSeq.ofSeq
                        |> AsyncSeq.iterAsync(fun fb ->
                            db
                            |> Cosmos.update fb.LogId fb.UserId (updateDiagEntry fb.Feedback)
                            |> Cosmos.execAsync
                            |> AsyncSeq.iter (fun _ -> ())
                    )
                with ex ->
                    Env.logException (ex,"writeLog")
            | None -> ()
        }

    let private channel = Channel.CreateBounded<LogEntry>(BoundedChannelOptions(BUFFER_SIZE,FullMode = BoundedChannelFullMode.DropOldest))

    //background loop to read channel and write diagnostics entry to backend
    let private consumerLoop =
        asyncSeq {
            while true do
                let! data = channel.Reader.ReadAsync().AsTask() |> Async.AwaitTask
                yield data
        }
        |> AsyncSeq.bufferByCountAndTime 10 BUFFER_WAIT
        |> AsyncSeq.iterAsync (fun entries -> async {
            let diagEntries = entries |> Array.choose (function Diag de -> Some de | _ -> None)
            let feedbackEntries = entries |> Array.choose (function Feedback fb -> Some fb | _ -> None)
            do! writeDiagAsync diagEntries
            do! updateWithFeedbackAsync feedbackEntries
        })
        |> Async.Start

    let write logEntry = channel.Writer.WriteAsync logEntry |> ignore

