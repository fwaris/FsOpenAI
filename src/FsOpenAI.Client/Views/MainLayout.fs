namespace FsOpenAI.Client.Views
open System
open Bolero.Html
open MudBlazor
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
                comp<PageTitle> { text (model.appConfig.AppName |> Option.defaultValue "") }
                comp<RadzenTheme> { "Theme" => this.ThemeService.Theme }
                comp<RadzenDialog>{attr.empty()}
                comp<RadzenComponents>{attr.empty()}
                comp<RadzenLayout> {
                    comp<RadzenNotification> { attr.empty() }
                    ecomp<HeaderView,_,_> model dispatch {attr.empty()}
                    ecomp<SidebarView,_,_> model dispatch {attr.empty()}
                    comp<RadzenBody> {
                        comp<RadzenSplitter> {
                            //"Style" => "height: calc(100vh - 1.0rem);"
                            "Style" => "height: 100%;"
                            "Orientation" => Orientation.Horizontal                    
                            comp<RadzenSplitterPane> {            
                                "Size" => "75%"
                                ecomp<ChatHistoryView,_,_> model dispatch {attr.empty()}
                            }
                            comp<RadzenSplitterPane> {
                                "Size" => "25%"    
                                attr.``class`` "rz-p-0 rz-p-lg-12"
                                "Style" => "overflow:auto;"
                                match Model.selectedChat model with
                                | Some chat ->  ecomp<SourcesView,_,_> (chat, model) dispatch {attr.empty()}
                                | None -> 
                                    comp<RadzenStack> {
                                        attr.``class`` "rz-p-8"
                                        comp<RadzenText> {
                                            attr.``class`` "rz-color-info-light"
                                            "TextStyle" => TextStyle.Caption
                                            "Text" => "Sources ..." 
                                        }
                                }
                            }
                        }
                    }
                    Footer.view this.JSRuntime model dispatch
                }
            }

(*
                comp<MudThemeProvider> { "isDarkMode" => model.darkTheme; "Theme" => model.theme }
                comp<MudPopoverProvider>
                comp<MudScrollToTop> {comp<MudFab> { "Icon" => Icons.Material.Filled.ArrowUpward; "Color" => Color.Primary; "Size" => Size.Small }}
                comp<MudDialogProvider> {attr.empty()}
                comp<MudSnackbarProvider> {attr.empty()}                
                comp<MudLayout> {
                    "Style" => "height:100vh; overflow: hidden; justify-content: space-around; display:flex"
                    match model.appConfig.AppBarType with
                    | Some (AppB_Base t) -> AppBar.appBar model t dispatch
                    | Some (AppB_Alt t)  -> AltAppBar.appBar model t dispatch
                    | None               -> ()            
                    comp<MudMainContent> {
                        ecomp<MainContent,_,_> model dispatch {attr.empty()}
                    }
                    comp<MudPaper> {                                            
                        "Class" => "fixed"
                        "Elevation" => 0
                        "Style" => $"height:11rem; width:100%%; max-width:{qwidth}; bottom:0; background:transparent; {qmargin}"
                        ecomp<QuestionView,_,_> model dispatch {attr.empty()} 
                    }
                    FooterBar.footer this.JSRuntime model dispatch
                }         
            }
*)
