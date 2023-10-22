namespace FsOpenAI.Client
open System
open System.Net.Http
open System.Security.Claims
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
open MudBlazor
open FsOpenAI.Client.Views

module App =
    let router = Router.infer SetPage (fun model -> model.page)

    let view model dispatch =
        ecomp<MainLayout,_,_> model dispatch {attr.empty()}
        //MainLayout.view model dispatch //homePage model dispatch

    type MyApp() =
        inherit ProgramComponent<Model, Message>()

        [<Inject>]
        member val LocalStore : Blazored.LocalStorage.ILocalStorageService = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val Snackbar : ISnackbar = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val logger:ILoggerProvider = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val Auth : AuthenticationStateProvider = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val HttpFac : IHttpClientFactory = Unchecked.defaultof<_> with get, set

        member val hubConn : HubConnection = Unchecked.defaultof<_> with get, set

        override this.Program =

            //authentication 
            let handler = new AuthenticationStateChangedHandler(fun t -> 
                task {
                    let! s = t              
                    this.Dispatch (SetAuth (Some s.User)) 
                } |> ignore)
            this.Auth.add_AuthenticationStateChanged(handler)

            //hub connection
            this.hubConn <- ClientHub.connection this.logger this.NavigationManager 
            let clientDispatch msg = this.Dispatch (FromServer msg) 
            let serverDispatch = ClientHub.send this.Dispatch this.hubConn
            let serverCall = ClientHub.call this.hubConn
            this.hubConn.On<ServerInitiatedMessages>(ClientHub.fromServer,clientDispatch) |> ignore

            let uparms =
                {
                    localStore = this.LocalStore
                    snkbar = this.Snackbar
                    serverDispatch = serverDispatch
                    serverCall = serverCall
                    navMgr = this.NavigationManager
                    httpFac = this.HttpFac
                }

            let update = Update.update uparms

            Program.mkProgram (fun _ -> Update.initModel, Cmd.ofMsg Init) update view 
            |> Program.withSubscription Subscription.asyncMessages
            |> Program.withRouter router
    #if DEBUG
            |> Program.withHotReload
            //|> Program.withConsoleTrace
    #endif
