module FsOpenAI.Client.Subscription
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.SignalR.Client
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Extensions.DependencyInjection

let subEndpoint = "/wschannel"

type SubMsg =
    | SetKey of string*string*string

let serOptions (o:JsonSerializerOptions)= 
    //o.Converters.Add(PcmdTimeConverter())
    //o.Converters.Add(NodenameConverter())
    JsonFSharpOptions.Default()
        .WithAllowNullFields(true)
        .WithAllowOverride(true)
        .AddToJsonSerializerOptions(o)                
    o


let subscription (loggerProvider: ILoggerProvider) (navMgr:NavigationManager) (dispatch: _ -> unit) =
    let hubConnection =
        HubConnectionBuilder()               
            .AddJsonProtocol(fun o -> serOptions o.PayloadSerializerOptions |> ignore)//.Converters.Add (JsonFSharpConverter()))
            .WithUrl(navMgr.ToAbsoluteUri(subEndpoint))
            .WithAutomaticReconnect()           
            .ConfigureLogging(fun logging ->
                logging.AddProvider(loggerProvider) |> ignore
            )           
            .Build()

    hubConnection.On<SubMsg> ("ChartUpdate", dispatch) |> ignore
    (hubConnection.StartAsync()) |> Async.AwaitTask |> Async.Start
    hubConnection
    

