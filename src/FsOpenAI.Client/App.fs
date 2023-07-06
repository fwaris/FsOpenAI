namespace FsOpenAI.Client
open System
open Elmish
open Bolero
open Bolero.Html
open Bolero.Remoting
open Bolero.Remoting.Client
open Bolero.Templating.Client
open Microsoft.Extensions.Logging
open FsOpenAI.Client.Views

module App =

    let view model dispatch =
        MainLayout.view model dispatch //homePage model dispatch

    type MyApp() =
        inherit ProgramComponent<Model, Message>()

        override this.Program =
            Program.mkProgram (fun _ -> Update.initModel, Cmd.none) Update.update view
            |> Program.withSubscription Update.asyncMessages
    #if DEBUG
            |> Program.withHotReload
    #endif
