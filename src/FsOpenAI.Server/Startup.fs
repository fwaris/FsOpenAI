namespace FsOpenAI.Server
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.Identity.Web
open Bolero.Remoting.Server
open Bolero.Server
open Bolero.Templating.Server
open MudBlazor.Services
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.AzureAppServices
open Blazored.LocalStorage

module Startup =

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    let configureServices (builder:WebApplicationBuilder) =
        let services = builder.Services

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd")) |> ignore

        services.AddMvc() |> ignore
        services.AddServerSideBlazor() |> ignore
        services.AddMudServices() |> ignore
        services.AddBlazoredLocalStorage() |> ignore
        services.AddControllersWithViews() |> ignore
        services.AddRazorPages() |> ignore
        
        services
            .AddSignalR(fun o -> o.MaximumReceiveMessageSize <- 5_000_000)
            .AddJsonProtocol(fun o ->FsOpenAI.Client.ClientHub.configureSer o.PayloadSerializerOptions |> ignore) |> ignore

        services
            .AddAuthorization()            
            .AddLogging(fun logging -> logging.AddConsole().AddDebug().AddAzureWebAppDiagnostics() |> ignore)
            //.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            //    .AddCookie()
            //    .Services
            ////.AddBoleroRemoting<Services.KeyService>()

            .AddBoleroHost(prerendered=false)
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
                endpoints.MapHub<ServerHub>(FsOpenAI.Client.ClientHub.urlPath) |> ignore
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

