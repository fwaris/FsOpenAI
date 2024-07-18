namespace FsOpenAI.Client.Views
open System
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open Microsoft.AspNetCore.Components.Web

module AppBar =

    let appBar model title dispatch =
        let bg =
            if model.darkTheme then
                model.appConfig.PaletteDark
                |> Option.bind (fun x -> x.AppBar)
                |> Option.defaultValue Colors.BlueGray.Darken3
            else
                model.appConfig.PaletteLight
                |> Option.bind (fun x -> x.AppBar)
                |> Option.defaultValue Colors.BlueGray.Lighten1

        comp<MudAppBar> {
            "Style" => $"background:{bg};"
            "Fixed" => true
            "Dense" => true
            comp<MudGrid> {
                //logo
                comp<MudItem> {
                    "xs" => 2
                    "sm" => 2
                    comp<MudLink> {
                        "Href" => match model.appConfig.LogoUrl with Some x -> x | None -> "#"
                        "Target" => "_blank"
                        comp<MudImage> {
                            "Class" => "mt-2"
                            "ObjectFit" => ObjectFit.Contain
                            //"Height" => Nullable 25
                            "Width" => Nullable 160
                            "Src" => $"app/imgs/logo.png"
                        }
                    }
                }
                //spinner
                comp<MudItem> {
                    "xs" => 1
                    "sm" => 1
                    //"Class" => "d-flex justify-center align-content-center flex-grow-0"
                    concat {
                        if model.busy then
                            comp<MudProgressCircular> {
                                "Class" => "mt-4"
                                "Color" => Color.Tertiary
                                "Indeterminate" => true
                                "Size" => Size.Small
                            }
                    }
                }
                //header
                comp<MudItem> {
                    "xs" => 6
                    "sm" => 6
                    comp<MudText> {
                        "Typo" => Typo.h5
                        "Align" => Align.Center
                        "Class" => "mt-3"
                        text title
                    }
                }
                //login
                comp<MudItem> {
                    "xs" => 1
                    "sm" => 1
                    if model.appConfig.RequireLogin then
                        ecomp<AvatarView,_,_> model dispatch {attr.empty()}
                }
                //add chat
                comp<MudItem> {
                    "xs" => 1
                    "sm" => 1
                    comp<MudFab> {
                        "StartIcon" => Icons.Material.Filled.Add
                        "IconSize" => Size.Large
                        "Color" => Color.Primary
                        on.click(fun _ -> dispatch (OpenCloseSettings C.ADD_CHAT_MENU))
                    }
                }
                //burger
                comp<MudItem> {
                    "xs" => 1
                    "sm" => 1
                    comp<MudMenu> {
                        "Icon" => Icons.Material.Filled.Menu
                        "IconSize" => Size.Small
                        "Class" => "mt-1"
                        //"TransformOrigin" => Origin.TopRight
                        //"Disabled" => (model.appConfig.RequireLogin && (match model.user with Unauthenticated -> true | _ -> false))
                        comp<MudPaper> {
                            "Style" => "margin-top:1rem;"
                            comp<MudMenuItem> {
                                "Icon" => if model.darkTheme then Icons.Material.Outlined.WbSunny else Icons.Material.Outlined.Nightlight
                                on.click(fun _ -> dispatch ToggleTheme)
                                "Toggle theme"
                            }
                            comp<MudMenuItem> {
                                "Icon" => Icons.Material.Outlined.DeleteSweep
                                on.click(fun _ -> dispatch Ia_ClearChats)
                                "Clear Chats"
                            }
                            comp<MudMenuItem> {
                                "Icon" => Icons.Material.Outlined.FolderDelete
                                "IconColor" => Color.Warning
                                on.click (fun _ -> dispatch PurgeLocalData)
                                "Purge all data stored in local browser storage"
                            }
                            if model.appConfig.EnabledBackends |> List.contains OpenAI then
                                comp<MudMenuItem> {
                                    "Icon" => Icons.Material.Outlined.Settings
                                    on.click(fun _ -> dispatch (OpenCloseSettings C.MAIN_SETTINGS))
                                    "Application Settings"
                                }
                        }             
                    }
                }                  
            }        
            ecomp<MainSettingsView,_,_> model dispatch {attr.empty()}
            ecomp<ChatCreateView,_,_> model dispatch {attr.empty()}
        }
