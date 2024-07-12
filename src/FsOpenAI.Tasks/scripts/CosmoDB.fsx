#load "ScriptEnv.fsx"
open System
open FSharp.CosmosDb
open FSharp.Control
//example on how to pull log data from Azure CosmosDB

type SREf = {[<Id>] id:string; [<PartitionKey>] UserId:string; Timestamp:DateTime}

let baseSettingsFile = @"%USERPROFILE%/.fsopenai/poc/ServiceSettings.json"
ScriptEnv.installSettings baseSettingsFile
let cstr = ScriptEnv.settings.Value.LOG_CONN_STR.Value

let db() =
    Cosmos.fromConnectionString cstr
    |> Cosmos.database FsOpenAI.GenAI.Monitoring.COSMOSDB
    |> Cosmos.container FsOpenAI.Shared.C.DFLT_MONITOR_TABLE_NAME

let findDrops() =
    db()
    |> Cosmos.query<SREf>("select c.id, c.UserId, c.Timestamp from c where c.AppId = '<your app id>'")
    |> Cosmos.execAsync
    |> AsyncSeq.toBlockingSeq
    |> Seq.toList

let drop (drops:SREf list) =
    drops
    |> AsyncSeq.ofSeq
    |> AsyncSeq.iterAsync (fun c -> 
        db() 
        |> Cosmos.deleteItem<SREf> c.id c.UserId 
        |> Cosmos.execAsync 
        |> AsyncSeq.iterAsync Async.Ignore)
    |> Async.Start

let toDrop = findDrops()

drop toDrop
