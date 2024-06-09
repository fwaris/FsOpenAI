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

type MainLayout() =
    inherit ElmishComponent<Model,Message>()    

    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set

    member this.CopyToClipboard(text:string) =
        this.JSRuntime.InvokeVoidAsync ("navigator.clipboard.writeText", text) |> ignore

    override this.View model dispatch =        

        match model.page with 
        | Page.Authentication action -> 
            ecomp<LoginRedirectView,_,_> (action,model) dispatch {attr.empty()}

        | Page.Home -> 
            let qwidth,qmargin = if model.tabsUp then "70rem","" else "60rem;","padding-left: 10rem;"
            concat {
                comp<PageTitle> { text (model.appConfig.AppName |> Option.defaultValue "") }
                comp<MudThemeProvider> { "isDarkMode" => model.darkTheme; "Theme" => model.theme }
                comp<MudScrollToTop> {comp<MudFab> { "Icon" => Icons.Material.Filled.ArrowUpward; "Color" => Color.Primary; "Size" => Size.Small }}
                comp<MudDialogProvider> {attr.empty()}
                comp<MudSnackbarProvider> {attr.empty()}                
                comp<MudLayout> {
                    "Style" => "height:100vh; overflow: hidden; justify-content: space-around; display:flex"
                    match model.appConfig.AppBarType with
                    | Some (AppB_Base t) -> AppBar.appBar model t dispatch
                    | Some (AppB_Alt t)  -> GCAppBar.appBar model t dispatch
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
