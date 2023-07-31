namespace FsOpenAI.Client
open System
open Elmish
open Bolero
open Bolero.Html
open Bolero.Remoting.Client
open Bolero.Templating.Client
open FsOpenAI.Client.Views
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Logging
open MudBlazor
open Microsoft.AspNetCore.SignalR.Client

module App =

    let view model dispatch =
        ecomp<MainLayout,_,_> model dispatch {attr.empty()}
        //MainLayout.view model dispatch //homePage model dispatch

    type MyApp() =
        inherit ProgramComponent<Model, Message>()

        [<Inject>]
        member val Snackbar : ISnackbar = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val logger:ILoggerProvider = Unchecked.defaultof<_> with get, set

        member val hubConn : HubConnection = Unchecked.defaultof<_> with get, set

        override this.Program =

            this.hubConn <- ClientHub.connection this.logger this.NavigationManager 
            let clientDispatch msg = this.Dispatch (FromServer msg) 
            let serverDispatch = ClientHub.send this.hubConn
            this.hubConn.On<ServerInitiatedMessages>(ClientHub.fromServer,clientDispatch) |> ignore

            Program.mkProgram (fun _ -> Update.initModel, Cmd.ofMsg Started) (Update.update this.Snackbar serverDispatch) view 
            |> Program.withSubscription Subscription.asyncMessages
    #if DEBUG
            |> Program.withHotReload
    #endif
