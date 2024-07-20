namespace FsOpenAI.GenAI


type CosmosDbConnection = {
    ConnectionString : string
    DatabaseName : string
    ContainerName : string
}

module Connection =
    open FSharp.CosmosDb
    open FSharp.Control

    let tryCreate(cstr,database,container) =
        try
            let db =
                Cosmos.fromConnectionString cstr
                |> Cosmos.database database
            do
                db
                |> Cosmos.createDatabaseIfNotExists
                |> Cosmos.execAsync
                |> AsyncSeq.iter (printfn "%A")
                |> Async.RunSynchronously
            Some { ConnectionString = cstr; DatabaseName = database; ContainerName = container }
        with ex ->
            Env.logException (ex,"Monitoring.installTable: ")
            None

