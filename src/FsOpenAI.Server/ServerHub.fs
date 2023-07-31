namespace FsOpenAI.Server
open System.Threading.Tasks
open FSharp.Control
open FsOpenAI.Client
open Microsoft.AspNetCore.SignalR

type ServerHub() =
    inherit Hub()

    static member SendMessage(client:ISingleClientProxy, msg:ServerInitiatedMessages) =
        task {
            return! client.SendAsync(ClientHub.fromServer,msg)            
        }

    member this.FromClient(msg:ClientInitiatedMessages) : Task = 
        let cnnId = this.Context.ConnectionId
        let client = this.Clients.Client(cnnId)
        task{
            match msg with 

            | Clnt_Connected _ -> 
                try
                    let! parms = Env.getParameters()
                    do! ServerHub.SendMessage(client,Srv_Parameters(parms))
                with ex ->
                    do! ServerHub.SendMessage(client,Srv_Error(ex.Message))

            | Clnt_StreamChat (settings,chat) ->
                let dispatch msg = ServerHub.SendMessage(client,msg) |> ignore
                Completions.completeChat settings chat dispatch |> Async.Start                

            | Clnt_RefreshIndexes (settings,initial) ->
                let! idx,err = Indexes.fetch settings
                do! ServerHub.SendMessage(client,Srv_IndexesRefreshed(idx,err,initial))

            | Clnt_StreamAnswer (settings,chat) ->
                let dispatch msg = ServerHub.SendMessage(client,msg) |> ignore
                QnA.runPlan settings chat dispatch |> Async.Start
        }
