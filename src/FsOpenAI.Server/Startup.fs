namespace FsOpenAI.Server
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Identity.Web
open Bolero.Remoting.Server
open Bolero.Server
open Bolero.Templating.Server
open Microsoft.Extensions.Logging
open Blazored.LocalStorage
open FsOpenAI.GenAI
open Radzen

module Startup =
    let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    let configureServices (builder:WebApplicationBuilder) =
        let services = builder.Services

        services
            .AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd")
            // .EnableTokenAcquisitionToCallDownstreamApi()
            // .AddInMemoryTokenCaches()
            |> ignore
        
        services.AddMvc() |> ignore
        services.AddServerSideBlazor() |> ignore
        services.AddRadzenComponents() |> ignore
        services.AddBlazoredLocalStorage() |> ignore
        services.AddControllersWithViews() |> ignore
        services.AddRazorPages() |> ignore
        services.AddMsalAuthentication(fun o -> ()) |> ignore
        services.AddHostedService<BackgroundTasks>() |> ignore
        
        services
            .AddSignalR(fun o -> o.MaximumReceiveMessageSize <- 1_000_000; )
            .AddJsonProtocol(fun o ->FsOpenAI.Client.ClientHub.configureSer o.PayloadSerializerOptions |> ignore) |> ignore

        services            
            .AddAuthorization()            
            .AddLogging()
            .AddBoleroHost(prerendered=false) //**** NOTE: MSAL authenication works on client side only so set prerendered=false 
            //.AddBoleroHost() //**** for debugging only
#if DEBUG
            .AddHotReload(templateDir = __SOURCE_DIRECTORY__ + "/../FsOpenAI.Client")
#endif
        |> ignore

    let configureApp (app:WebApplication) =        
        let env = app.Environment
        
        //configuration
        let config = app.Services.GetRequiredService<IConfiguration>()
        let logger = app.Services.GetRequiredService<ILogger<FsOpenAILog>>()       
        Env.init(config,logger,env.WebRootPath)        

        app
            .UseWebSockets() |> ignore

        if env.IsDevelopment() then
            app.UseWebAssemblyDebugging()
        else
            app.UseHsts() |> ignore

        app            
            .UseHttpsRedirection()
            .UseMiddleware<TokenHandler>()
            .UseAuthentication()
            .UseStaticFiles()
            .UseRouting()
            .UseAuthorization()
            .UseBlazorFrameworkFiles()
            .UseEndpoints(fun endpoints ->
#if DEBUG
                endpoints.UseHotReload()
#endif
                endpoints.MapBlazorHub() |> ignore
                endpoints.MapBoleroRemoting() |> ignore
                endpoints.MapHub<ServerHub>(FsOpenAI.Shared.C.ClientHub.urlPath) |> ignore
                endpoints.MapFallbackToBolero(Index.page) |> ignore)
        |> ignore

module Program =

    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)
        Startup.configureServices builder
        let app = builder.Build()
        Startup.configureApp app
        app.Run()
        0

