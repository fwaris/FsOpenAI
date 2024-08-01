namespace FsOpenAI.Client
open System
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.Extensions.DependencyInjection
open FSharp.Control
open FsOpenAI.Shared

module ClientHub =
    open System.Threading.Channels

    let configureSer (o:JsonSerializerOptions)=
        JsonFSharpOptions.Default()
            .WithAllowNullFields(true)
            .WithAllowOverride(true)
            .AddToJsonSerializerOptions(o)
        o

    let retryPolicy = [| TimeSpan(0,0,5); TimeSpan(0,0,10); TimeSpan(0,0,30); TimeSpan(0,0,30) |]

    //signalr hub connection that can send/receive messages to/from server
    let connection (loggerProvider: ILoggerProvider) (navMgr:NavigationManager)  =
        let hubConnection =
            HubConnectionBuilder()
                .AddJsonProtocol(fun o -> configureSer o.PayloadSerializerOptions |> ignore)
                .WithUrl(
                        navMgr.ToAbsoluteUri(C.ClientHub.urlPath),
                        (fun w ->
                            w.Transports <- Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling
                            //w.CloseTimeout <- TimeSpan.FromSeconds(5.0)
                        )
                    )
                .WithAutomaticReconnect(retryPolicy)
                .ConfigureLogging(fun logging ->
                    logging.AddProvider(loggerProvider) |> ignore
                )
                .Build()
        hubConnection.KeepAliveInterval <- TimeSpan.FromSeconds(10.0)
        (hubConnection.StartAsync()) |> Async.AwaitTask |> Async.Start
        hubConnection

    let rec retrySend count (conn:HubConnection) (msg:ClientInitiatedMessages) =
        if count < 7 then
            async {
                printfn $"try resend message {count + 1}"
                try
                    if conn.State = HubConnectionState.Connected then
                        do! conn.SendAsync(C.ClientHub.fromClient,msg) |> Async.AwaitTask
                    else
                        do! Async.Sleep 1000
                        return! retrySend (count+1) conn msg
                with ex ->
                        do! Async.Sleep 1000
                        return! retrySend (count+1) conn msg
            }
        else
            async {
                printfn $"retry limit reached of {count}"
                return ()
            }

    let send clientDispatch (conn:HubConnection) (msg:ClientInitiatedMessages) =
        task {
            try
                if conn.State = HubConnectionState.Connected then
                    printfn "sending message"
                    do! conn.SendAsync(C.ClientHub.fromClient,msg)
                else
                    retrySend 0 conn msg |> Async.Start
            with ex ->
                retrySend 0 conn msg |> Async.Start
                clientDispatch (ShowError ex.Message)
        }
        |> ignore

    let call (conn:HubConnection) (msg:ClientInitiatedMessages) =
        conn.SendAsync(C.ClientHub.fromClient,msg)

