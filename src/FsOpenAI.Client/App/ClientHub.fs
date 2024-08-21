namespace FsOpenAI.Client
open System
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.WebAssembly.Authentication
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.Extensions.DependencyInjection
open FSharp.Control
open FsOpenAI.Shared

module ClientHub =

    let configureSer (o:JsonSerializerOptions)=
        JsonFSharpOptions.Default()
            .WithAllowNullFields(true)
            .WithAllowOverride(true)
            .AddToJsonSerializerOptions(o)
        o

    let getToken (accessTokenProvider:IAccessTokenProvider) () = 
        task {
            if accessTokenProvider = null then 
                printfn "access token provider not set"
                return null
            else
                let! token = accessTokenProvider.RequestAccessToken()
                match token.TryGetToken() with 
                | true, token -> printfn $"have access token; expires: {token.Expires}"; return token.Value
                | _ -> printfn "don't have access token"; return null
        }

    let retryPolicy = [| TimeSpan(0,0,5); TimeSpan(0,0,10); TimeSpan(0,0,30); TimeSpan(0,0,30) |]
        
    //signalr hub connection that can send/receive messages to/from server
    let connection 
        (tokenProvider:IAccessTokenProvider) 
        (loggerProvider: ILoggerProvider) 
        (navMgr:NavigationManager)  
        =
        let hubConnection =
            HubConnectionBuilder()
                .AddJsonProtocol(fun o -> configureSer o.PayloadSerializerOptions |> ignore)
                .WithUrl(
                        navMgr.ToAbsoluteUri(C.ClientHub.urlPath),
                        fun w -> 
                            w.AccessTokenProvider <- (getToken tokenProvider)
                            //w.Transports <- Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets
                            )
                .WithAutomaticReconnect(retryPolicy)
                .ConfigureLogging(fun logging ->
                    logging.AddProvider(loggerProvider) |> ignore
                )
                .Build()
        (hubConnection.StartAsync()) |> Async.AwaitTask |> Async.Start
        hubConnection

    let reconnect (conn:HubConnection) = 
        task {
            try
                do! conn.StopAsync()
                do! conn.StartAsync()
                printfn "hub reconnected"
            with ex ->
                printfn $"hub reconnect failed {ex.Message}" 
        } |> ignore
        
    let rec private retrySend methodName count (conn:HubConnection) (msg:ClientInitiatedMessages) =
        if count < 7 then
            async {
                printfn $"try resend message {count + 1}"
                try
                    if conn.State = HubConnectionState.Connected then
                        do! conn.SendAsync(methodName,msg) |> Async.AwaitTask
                    else
                        do! Async.Sleep 1000
                        return! retrySend methodName (count+1) conn msg
                with ex ->
                        do! Async.Sleep 1000
                        return! retrySend methodName (count+1) conn msg
            }
        else
            async {
                printfn $"retry limit reached of {count}"
                return ()
            }

    let private _send invokeMethod clientDispatch (conn:HubConnection) (msg:ClientInitiatedMessages) =
        task {
            try
                if conn.State = HubConnectionState.Connected then                    
                    do! conn.SendAsync(invokeMethod,msg)
                else
                    retrySend invokeMethod 0 conn msg |> Async.Start
            with ex ->
                retrySend invokeMethod 0 conn msg |> Async.Start
                clientDispatch (ShowError ex.Message)
        }
        |> ignore

    let send clientDispatch (conn:HubConnection) (msg:ClientInitiatedMessages) = 
        _send C.ClientHub.fromClient clientDispatch conn msg

    //allows unauthenticated users access to initial settings
    let sendUnAuth clientDispatch (conn:HubConnection) (msg:ClientInitiatedMessages) = 
        _send C.ClientHub.fromClientUnAuth clientDispatch conn msg

    let call (conn:HubConnection) (msg:ClientInitiatedMessages) =
        conn.SendAsync(C.ClientHub.fromClient,msg)

