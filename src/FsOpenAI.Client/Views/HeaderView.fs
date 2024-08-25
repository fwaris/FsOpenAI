namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared
open Radzen
open Radzen.Blazor
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web

type HeaderView() =
    inherit ElmishComponent<Model,Message>()
    let transparentBg = "background: transparent;"

    [<Inject>] member val ThemeService:ThemeService = Unchecked.defaultof<_> with get,set

    override this.View model dispatch = 
        let sidebarExpanded = TmpState.isOpenDef true C.SIDE_BAR_EXPANDED model
        comp<RadzenHeader> {
            //attr.``class`` "rz-background-color-danger-dark"
            comp<RadzenRow> {
                "AlignItems" => AlignItems.Center
                comp<RadzenColumn> {
                    "Size" => 1
                    comp<RadzenSidebarToggle> {
                        //"Style" => transparentBg   
                        "Icon" => (if sidebarExpanded then "chevron_left" else  "chevron_right")
                        attr.callback "Click" (fun (e:EventArgs) -> dispatch ToggleSideBar)
                    }
                }
                comp<RadzenColumn> {
                    "Size" => 2
                    if model.busy then
                        comp<RadzenProgressBarCircular> {
                            "ShowValue" => false
                            "Mode" => ProgressBarMode.Indeterminate
                            "Size" => ProgressBarCircularSize.Small
                            "ProgressBarStyle" => ProgressBarStyle.Danger
                        }
                }
                comp<RadzenColumn>{
                    "Size" => 5
                    comp<RadzenText> {
                        "Style" => "text-align: center; width: 100%; align-self: center;"
                        "Text" => (model.appConfig.AppName |> Option.defaultValue "FsOpenAI")
                        "TextStyle" => TextStyle.H6
                    }
                }
                comp<RadzenColumn>{
                    "Size" => 1
                    let isAuthenticated = match model.user with | Authenticated _ -> true | _ -> false                
                    if model.appConfig.RequireLogin then 
                        if not isAuthenticated then 
                            comp<RadzenButton> {
                                "Text" => "Login"                        
                                attr.callback "Click" (fun (e:MouseEventArgs) -> dispatch LoginLogout)
                            }
                        else 
                            comp<RadzenProfileMenu> {
                                attr.fragment "Template" ( 
                                    comp<RadzenImage> {
                                    "Path" => (model.photo |> Option.defaultValue "imgs/person.png")
                                    "Style" => "width: 1.5rem; border-radius: 50%;"
                                    })
                                comp<RadzenProfileMenuItem> {
                                    "Text" => match model.user with 
                                                | Unauthenticated  -> "Login"
                                                | Authenticated user -> $"Log out {user.Name}"
                                }                    
                            }
                }
                comp<RadzenColumn> {
                    "Size" => 1
                    comp<RadzenAppearanceToggle > {attr.empty()}
                }
                comp<RadzenColumn> {
                    "Size" => 2
                    ecomp<MainSettingsView,_,_> model dispatch {attr.empty()}
                }
            }
        }
