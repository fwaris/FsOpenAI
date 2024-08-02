namespace FsOpenAI.Server
open System
open System.Threading.Tasks
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
open FsOpenAI.GenAI
open FsOpenAI.Shared

module Startup =
    let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

    let jwtBearerEvents() =
        let evts = JwtBearerEvents()

        evts.OnMessageReceived <-
            (fun (context:MessageReceivedContext) -> 
                let accessToken = context.Request.Query.["access_token"]
                let path = context.HttpContext.Request.Path
                if not(String.IsNullOrEmpty(accessToken)) && path.StartsWithSegments(C.ClientHub.urlPath) then
                    context.Token <- accessToken       
                    printfn "token found"                             
                Task.CompletedTask)

        evts.OnAuthenticationFailed <-
            (fun (context:AuthenticationFailedContext) ->
                printfn "Token validation failed: %s" context.Exception.Message
                Task.CompletedTask)
        evts

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    let configureServices (builder:WebApplicationBuilder) =
        let services = builder.Services

        services
            .AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd")
            // .AddAuthentication(fun o ->
            //     o.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
            //     o.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
            // .AddJwtBearer(fun o -> 
            //     o.Authority <- "https://login.microsoftonline.com/62f00f93-abe4-444c-a2f4-02b2de127943"
            //     //o.Audience <- "e6f10f3b-cbb4-44e5-b5b6-27dd3217e9bb"
            //     o.UseSecurityTokenValidators <- true
            //     o.Events <- jwtBearerEvents()
            //     o.TokenValidationParameters <- new Microsoft.IdentityModel.Tokens.TokenValidationParameters(
            //         ValidateIssuer = true,
            //         ValidIssuer = "https://login.microsoftonline.com/62f00f93-abe4-444c-a2f4-02b2de127943/v2.0",
            //         ValidateAudience = true,
            //         ValidAudience = "e6f10f3b-cbb4-44e5-b5b6-27dd3217e9bb",

            //         ValidateLifetime = true,
            //         ClockSkew = TimeSpan.Zero)
            // )
            |> ignore

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
            .AddBoleroHost(prerendered=false) //**** NOTE: MSAL authenication works on client side only so set prerendered=false 
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

