namespace FsOpenAI.Client
open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Bolero.Remoting.Client
open MudBlazor.Services
open Blazored.LocalStorage

module Program =

    [<EntryPoint>]
    let Main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        builder.RootComponents.Add<App.MyApp>("#main")
        builder.Services.AddBoleroRemoting(builder.HostEnvironment) |> ignore
        builder.Services.AddMudServices() |> ignore 
        builder.Services.AddBlazoredLocalStorage() |> ignore
        builder.Build().RunAsync() |> ignore
        0


