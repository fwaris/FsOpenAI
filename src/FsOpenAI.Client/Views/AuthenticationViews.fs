namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.WebAssembly.Authentication
open Microsoft.AspNetCore.Components.Web
open FsOpenAI.Shared

type LoginRedirectView() =
    inherit ElmishComponent<string*Model,Message>()

    [<Inject>]
    member val NavMgr : NavigationManager = Unchecked.defaultof<_> with get, set

    [<Inject>]
    member val ThemeService = Unchecked.defaultof<ThemeService> with get, set

    override this.View mdl (dispatch:Message -> unit) =
        let action,model = mdl
        concat {
                comp<PageTitle> { text (model.appConfig.AppName |> Option.defaultValue "") }
                comp<RadzenTheme> { "Theme" => this.ThemeService.Theme }
                comp<RadzenDialog>{attr.empty()}
                comp<RadzenComponents>{attr.empty()}
                comp<RadzenLayout> {
                    comp<RadzenBody> {
                        comp<RemoteAuthenticatorView> {
                            "Action" => action
                        }
                    }
                }
            }
