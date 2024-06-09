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

type DiagEntry = {
    [<Id>]
    id : string
    AppId : string
    [<PartitionKey>]
    UserId : string
    Prompt : PromptLog
    Response : string
    InputTokens : int
    OutputTokens : int
    Error : string
    Backend : string
    Resource : string
    Model : string
    Timestamp : DateTime
}

module Monitoring = 
    let BUFFER_SIZE = 1000
    let BUFFER_WAIT = 10000
    let COSMOSDB = "fsopenai"
    let mutable private _tableClient = lazy None

    let private installTable() =
        Env.logInfo "installing monitoring"
        try 
            Env.appConfig.Value
            |> Option.bind(fun x -> Env.logInfo $"{x.DiagTableName}"; x.DiagTableName)
            |> Option.bind(fun t -> Settings.getSettings().Value.LOG_CONN_STR |> Option.map(fun cstr -> Env.logInfo $"{t} - {Utils.shorten 30 cstr}";cstr,t))
            |> Option.bind(fun (cstr,t) -> 
            (*
            *)
                let db =
                    Cosmos.fromConnectionString cstr
                    |> Cosmos.database COSMOSDB

                do
                    db
                    |> Cosmos.createDatabaseIfNotExists
                    |> Cosmos.execAsync
                    |> AsyncSeq.iter (printfn "%A")
                    |> Async.RunSynchronously

                do 
                    db
                    |> Cosmos.container t                 
                    |> Cosmos.createContainerIfNotExists<DiagEntry>
                    |> Cosmos.execAsync
                    |> AsyncSeq.iter (printfn "%A")
                    |> Async.RunSynchronously

                Env.logInfo($"Logging diagnostics to {t}")
                Some(cstr,t))

            |> Option.orElseWith(fun () -> 
                Env.logError("unable to configure diagnostics - no connection string provided")
                None)
        with ex ->
            Env.logException (ex,"Monitoring.installTable: ")
            None

    let update() = _tableClient <- lazy(installTable())

    let private writeLogAsync (diagEntries:DiagEntry[]) =
        async {
            match _tableClient.Value with
            | Some (cstr,t) -> 
                try
                    do!
                        Cosmos.fromConnectionString cstr
                            |> Cosmos.database COSMOSDB
                            |> Cosmos.container t
                            |> Cosmos.upsertMany (Array.toList diagEntries)
                            |> Cosmos.execAsync
                            |> AsyncSeq.iter (fun _ -> ())
                with ex -> 
                    Env.logException (ex,"writeLog")
            | None -> ()
        }
        
    let private channel = Channel.CreateBounded<DiagEntry>(BoundedChannelOptions(BUFFER_SIZE,FullMode = BoundedChannelFullMode.DropOldest))

    //background loop to read channel and write diagnostics entry to backend
    let private consumerLoop =    
        asyncSeq {
            while true do
                let! data = channel.Reader.ReadAsync().AsTask() |> Async.AwaitTask
                yield data
        }
        |> AsyncSeq.bufferByCountAndTime 10 BUFFER_WAIT
        |> AsyncSeq.iterAsync writeLogAsync
        |> Async.Start

    let write diagEntry = channel.Writer.WriteAsync diagEntry |> ignore

