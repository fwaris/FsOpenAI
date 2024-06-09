namespace UISandbox.Client
open System
open System.Net.Http
open System.Security.Claims
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Logging
open Elmish
open Bolero
open Bolero.Html
open Bolero.Remoting.Client
open MudBlazor

module Update =
    let initModel = { x = "Hello World"; selectedChatId = None }

    let update message model =
        match message with
        | Ping -> model,Cmd.none

module App =

    let view model dispatch =
        ecomp<MainLayout,_,_> model dispatch {attr.empty()}

    type MyApp() =
        inherit ProgramComponent<Model, Message>()

        [<Inject>]
        member val Snackbar : ISnackbar = Unchecked.defaultof<_> with get, set

        [<Inject>]
        member val logger:ILoggerProvider = Unchecked.defaultof<_> with get, set

        override this.Program =

            Program.mkProgram (fun _ -> Update.initModel, Cmd.none) Update.update view 
    #if DEBUG
            //|> Program.withConsoleTrace
    #endif
