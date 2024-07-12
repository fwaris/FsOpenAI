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
open FsOpenAI.Shared

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
        let isUnauthenticated = match model.user with Unauthenticated -> true | _ -> false

        let badgeColor =
            match model.user with
            | Authenticated u when u.IsAuthorized -> Color.Tertiary
            | Authenticated _                     -> Color.Warning
            | Unauthenticated                     -> Color.Info

        concat {
            if model.appConfig.RequireLogin && isUnauthenticated then
                comp<MudButton> {
                    "Class" => "mt-2"
                    "Variant" => Variant.Filled
                    "Color" => Color.Secondary
                    on.click (fun _ -> dispatch LoginLogout)
                    text "Login"
                }
            else
                comp<MudMenu> {
                    "Class" => "mt-4"
                    "Origin" => Anchor.Top
                    attr.fragment "ActivatorContent" (
                        comp<MudBadge> {
                            "Elevation" => 10
                            "Color" => badgeColor
                            "Overlap" => false
                            "Bordered" => isUnauthenticated
                            "Dot" => not isUnauthenticated
                            "Origin" => Origin.CenterRight
                            comp<MudAvatar> {
                                "Size" => Size.Small
                                comp<MudImage> {
                                    "Src" => (match model.photo with Some s -> s | _ -> "imgs/person.png")
                                }
                            }
                        }
                    )
                    comp<MudMenuItem> {
                        on.click (fun e -> dispatch LoginLogout)
                        match model.user with
                        | Authenticated u -> $"Logout {u.Name}"
                        | _               -> "Login"
                    }
                }
        }
