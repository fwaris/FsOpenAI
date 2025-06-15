namespace FsOpenAI.Client.Views
open System
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open Bolero
open Microsoft.AspNetCore.Components.Web
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop
open Radzen
open Radzen.Blazor

type MainLayout() =
    inherit ElmishComponent<Model,Message>()

    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set

    [<Inject>]
    member val ThemeService = Unchecked.defaultof<ThemeService> with get, set

    member this.CopyToClipboard(text:string) =
        this.JSRuntime.InvokeVoidAsync ("navigator.clipboard.writeText", text) |> ignore

    override this.OnParametersSet() =
        if this.ThemeService.Theme = null then
            this.ThemeService.SetTheme "Humanistic"

    override this.View model dispatch =

        match model.page with
        | Page.Authentication action ->
            ecomp<LoginRedirectView,_,_> (action,model) dispatch {attr.empty()}

        | Page.Home ->
            concat {
                comp<RadzenComponents>{attr.empty()}
                comp<PageTitle> { text (model.appConfig.AppName |> Option.defaultValue "") }
                comp<RadzenTheme> { "Theme" => this.ThemeService.Theme }
                comp<RadzenDialog>{attr.empty()}
                comp<RadzenLayout> {
                    comp<RadzenNotification> { attr.empty() }
                    ecomp<HeaderView,_,_> model dispatch {attr.empty()}
                    ecomp<SidebarView,_,_> model dispatch {attr.empty()}
                    comp<RadzenBody> {
                        comp<RadzenSplitter> {
                            "Style" => "height: 100%;"
                            "Orientation" => Orientation.Horizontal
                            comp<RadzenSplitterPane> {
                                "Size" => "75%"
                                ecomp<ChatHistoryView,_,_> model dispatch {attr.empty()}
                            }
                            comp<RadzenSplitterPane> {
                                "Size" => "25%"
                                attr.``class`` "rz-p-0 rz-p-lg-3"
                                "Style" => "overflow:auto;"
                                ecomp<SourcesView,_,_> model dispatch {attr.empty()}
                            }
                        }
                    }
                    Footer.view this.JSRuntime model dispatch
                }
            }
