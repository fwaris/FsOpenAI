namespace FsOpenAI.Client
open System
open System.Net.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open MudBlazor.Services
open Blazored.LocalStorage
open Bolero.Remoting.Client

module Program =
    open Microsoft.AspNetCore.Components.Web

    [<EntryPoint>]
    let Main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        builder.RootComponents.Add<App.MyApp>("#main")
        builder.RootComponents.Add<HeadOutlet>("head::after")
        builder.Services.AddBoleroRemoting(builder.HostEnvironment) |> ignore
        builder.Services.AddMudServices() |> ignore 
        builder.Services.AddBlazoredLocalStorage(fun o -> o.JsonSerializerOptions <- ClientHub.configureSer o.JsonSerializerOptions) |> ignore

        //http factory to create clients to call Microsoft graph api
        builder.Services.AddScoped<Graph.GraphAPIAuthorizationMessageHandler>() |> ignore
        builder.Services.AddHttpClient(
            Graph.Api.CLIENT_ID, 
            Action<HttpClient>(Graph.Api.configure)) //need type annotation to bind to the correct overload

            .AddHttpMessageHandler<Graph.GraphAPIAuthorizationMessageHandler>()
            |> ignore    

        //add authentication that internally uses the msal.js library
        builder.Services.AddMsalAuthentication(fun o -> 
                //read configuration to reference the AD-app
                builder.Configuration.Bind("AzureAd", o.ProviderOptions.Authentication)
                printfn "adding msal authentication"

                //NOTE: EntraID app registration should have a scope called API.Access in 'expose an api' section
                let defScope = $"api://{o.ProviderOptions.Authentication.ClientId}/API.Access"

                o.ProviderOptions.DefaultAccessTokenScopes.Add(defScope)
                ) |> ignore

        builder.Build().RunAsync() |> ignore
        0


