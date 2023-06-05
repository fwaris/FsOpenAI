module FsOpenAI.Client.App

open System
open Elmish
open Bolero
open Bolero.Html
open Bolero.Remoting
open Bolero.Remoting.Client
open Bolero.Templating.Client
open FsOpenAI.Client.Model
open Microsoft.Extensions.Logging

let view model dispatch =
    MainLayout.view model dispatch //homePage model dispatch

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let logger = this.Services.GetService(typeof<ILoggerProvider>) :?> ILoggerProvider
        let sub dispatch = Subscription.subscription logger  this.NavigationManager dispatch
        let sub (model:Model) : (SubId * Subscribe<Message>) list =
            let sub dispatch : IDisposable =
                let hub = sub (function Subscription.SetKey (a,b,c) -> dispatch (SetKeys (a,b,c)))
                {new IDisposable with member _.Dispose() = hub.DisposeAsync() |> ignore}
            [["key"],sub]
        //let keyServce = this.Remote<KeyService>()
        //let update = update bookService
        Program.mkProgram (fun _ -> initModel, Cmd.none) update view
        |> Program.withSubscription sub
        //|> Program.withRouter router
#if DEBUG
        |> Program.withHotReload
#endif
