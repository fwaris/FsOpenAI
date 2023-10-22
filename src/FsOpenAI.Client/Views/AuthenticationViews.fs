namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.WebAssembly.Authentication
open Microsoft.AspNetCore.Components.Web

type LoginRedirectView() =
    inherit ElmishComponent<string*Model,Message>()

    [<Inject>]
    member val NavMgr : NavigationManager = Unchecked.defaultof<_> with get, set

    override this.View mdl (dispatch:Message -> unit) =
        let action,model = mdl
        div {
            comp<MudThemeProvider> { "isDarkMode" => model.darkTheme }
            comp<MudGrid> {
                comp<MudItem> {
                    "xs" => 3
                    comp<MudLink> {                    
                        "Href" => this.NavMgr.BaseUri                    
                        comp<MudPaper> {
                            "Class" => "d-flex gap-4"
                            comp<MudIcon> {
                                "Class" => "align-self-center"
                                "Icon" => Icons.Material.Outlined.Home
                                "Size" => Size.Medium
                            }
                            comp<MudText> {
                                "Class" => "align-self-center"
                                "Typo" => Typo.button
                                "Back"
                            }
                        }
                    }
                }
                comp<MudItem> {
                    "xs" => 6
                    comp<MudText> {
                        "Class" => "align-self-center"
                        "Typo" => Typo.h6
                        "Logging in ... please wait ..."
                    }
                }
                //comp<MudItem> {
                //    "xs" => 3
                //    comp<MudSpacer> {attr.empty()}
                //}
            }
            //this component does all the login magic
            comp<RemoteAuthenticatorView> { 
                "Action" => action
            }
        }

type AvatarView() =
    inherit ElmishComponent<Model,Message>()

    override this.View model (dispatch:Message -> unit) = 

        let badgeColor = 
            match model.user with
            | Authenticated u when u.IsAuthorized -> Color.Tertiary
            | Authenticated _                     -> Color.Warning
            | Unauthenticated                     -> Color.Default

        comp<MudMenu> {
            "Class" => "mt-5"
            "Origin" => Anchor.Top
            attr.fragment "ActivatorContent" (
                comp<MudBadge> {
                    "Elevation" => 3
                    "Color" => badgeColor
                    "Overlap" => false
                    "Bordered" => false
                    "Dot" => true
                    "Origin" => Origin.CenterRight
                    comp<MudAvatar> {                            
                        "Image" => (match model.photo with Some s -> s | _ -> "imgs/person.png")
                        "Size" => Size.Small
                    }                            
                }
            )
            comp<MudMenuItem> {
                attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch LoginLogout)
                on.click (fun e -> dispatch LoginLogout)
                match model.user with 
                | Authenticated u -> $"Logout {u.Name}"
                | _               -> "Login"
            }
        }
