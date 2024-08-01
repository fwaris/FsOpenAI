namespace FsOpenAI.Client
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Components.WebAssembly.Authentication
open System.Text.Json.Serialization
open System.Text.Json
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

    let getToken (accessTokenProvider:IAccessTokenProvider) () = 
        task {
            let! token = accessTokenProvider.RequestAccessToken()
            match token.TryGetToken() with 
            | true, token -> printfn $"have token {token.Value}"; return token.Value
            | _ -> printfn "don't have token"; return null
        }
        
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
                    navMgr.ToAbsoluteUri(C.ClientHub.urlPath)
                    ,fun o -> 
                        o.AccessTokenProvider <- (getToken tokenProvider)
                    )
                .WithAutomaticReconnect()           
                .ConfigureLogging(fun logging ->
                    logging.AddProvider(loggerProvider) |> ignore
                )           
                .Build()
        (hubConnection.StartAsync()) |> Async.AwaitTask |> Async.Start
        hubConnection

    let reconnect (conn:HubConnection) = 
        task {
            do! conn.StopAsync()
            do! conn.StartAsync()
        } |> ignore


    let send clientDispatch (conn:HubConnection) (msg:ClientInitiatedMessages) = 
        task {
            try 
                do! conn.SendAsync(C.ClientHub.fromClient,msg)
            with ex -> 
                clientDispatch (ShowError ex.Message)
        }
        |> ignore

    let call (conn:HubConnection) (msg:ClientInitiatedMessages) = 
        conn.SendAsync(C.ClientHub.fromClient,msg) 

