namespace FsOpenAI.Client
open System.Net.Http
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Components.Authorization
open Microsoft.AspNetCore.Components.WebAssembly.Authentication
open Microsoft.AspNetCore.SignalR.Client
open Elmish
open Bolero
open Bolero.Html
open Bolero.Remoting.Client
open Bolero.Templating.Client
open FsOpenAI.Client.Views
open FsOpenAI.Shared

module App =
    open Radzen
    let router = Router.infer SetPage (fun model -> model.page)

    let view model dispatch =
        ecomp<MainLayout,_,_> model dispatch {attr.empty()}
        //MainLayout.view model dispatch //homePage model dispatch

    type MyApp() =
        inherit ProgramComponent<Model, Message>()

        [<Inject>]
        member val LocalStore : Blazored.LocalStorage.ILocalStorageService = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val logger:ILoggerProvider = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val Auth : AuthenticationStateProvider = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val HttpFac : IHttpClientFactory = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val TokenProvider : IAccessTokenProvider = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val NotificationService : NotificationService = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val DialogService : DialogService = Unchecked.defaultof<_> with get, set


        member val hubConn = ref None

        member this.Connect() = 
            task {                
                let! hc = ClientHub.connection this.TokenProvider this.logger this.NavigationManager 
                this.hubConn.Value <- Some hc
                let clientDispatch msg = this.Dispatch (FromServer msg) 
                hc.On<ServerInitiatedMessages>(C.ClientHub.fromServer,clientDispatch) |> ignore
            }

        override this.Program =

            //authentication 
            let handler = new AuthenticationStateChangedHandler(fun t -> 
                task {
                    let! s = t
                    this.Dispatch (SetAuth (Some s.User)) 
                } |> ignore)
            this.Auth.add_AuthenticationStateChanged(handler)

            let getInitConfig() =
                task {
                    let! v = this.JSRuntime.InvokeAsync<string>("inputValue", [|C.LOAD_CONFIG_ID|])
                    let cfg =
                        try
                            let str = v |> System.Convert.FromBase64String |> System.Text.Encoding.UTF8.GetString
                            System.Text.Json.JsonSerializer.Deserialize<LoadConfig>(str,Utils.serOptions())
                        with ex ->                             
                            LoadConfig.Default
                    return cfg
                }

            let uparms =
                {
                    localStore = this.LocalStore
                    notificationService = this.NotificationService
                    dialogService = this.DialogService
                    serverConnect = this.Connect
                    serverDispatch = ClientHub.send this.Dispatch this.hubConn
                    serverCall = ClientHub.call this.hubConn
                    navMgr = this.NavigationManager
                    httpFac = this.HttpFac
                    getInitConfig = getInitConfig
                }

            let update = Update.update uparms            

            Program.mkProgram (fun _ -> Model.initModel, Cmd.ofMsg GetInitConfig) update view 
            |> Program.withSubscription Subscription.asyncMessages
            |> Program.withRouter router
    #if DEBUG
            |> Program.withHotReload
            //|> Program.withConsoleTrace
    #endif
