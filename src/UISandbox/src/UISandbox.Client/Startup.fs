namespace UISandbox.Client

open System
open System.Net.Http
open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Components.Web
open MudBlazor.Services

module Program =

    [<EntryPoint>]
    let Main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        builder.RootComponents.Add<App.MyApp>("#main")
        builder.RootComponents.Add<HeadOutlet>("head::after")
        builder.Services.AddMudServices() |> ignore 
        builder.Services.AddScoped<HttpClient>(fun _ ->
            new HttpClient(BaseAddress = Uri builder.HostEnvironment.BaseAddress)) |> ignore
        builder.Build().RunAsync() |> ignore
        0
