namespace FsOpenAI.Client.Views
open System
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components.Web
open FsOpenAI.Shared

module GCAppBar =

    let appBar model title dispatch = 
        let bg = 
            if model.darkTheme then 
                model.appConfig.PaletteDark 
                |> Option.bind (fun x -> x.AppBar)
                |> Option.defaultValue Colors.BlueGrey.Darken3
            else
                model.appConfig.PaletteLight 
                |> Option.bind (fun x -> x.AppBar)
                |> Option.defaultValue Colors.BlueGrey.Lighten1

        comp<MudAppBar> {
            "Style" => $"background:{bg};"
            "Fixed" => true
            "Dense" => false
            comp<MudGrid> {
                comp<MudItem> {
                    "xs" => 1
                    "sm" => 1
                    comp<MudLink> {
                        "Href" => match model.appConfig.LogoUrl with Some x -> x | None -> "#"
                        "Target" => "_blank"
                        comp<MudImage> {
                            "Class" => "mt-2"
                            "ObjectFit" => ObjectFit.Contain
                            //"Height" => Nullable 25
                            "Width" => Nullable 47
                            "Src" => $"app/imgs/logo.png"
                        }
                    }
                }
                comp<MudItem> {
                    "xs" => 10                   
                    "sm" => 10          
                    comp<MudText> {
                        "Typo" => Typo.h4
                        "Align" => Align.Center
                        "Class" => "mt-3"
                        text title
                    }
                    //comp<MudPaper> {
                    //    "Class" => "d-none d-md-flex d-xs-none align justify-center"
                    //    "Style" => "background: transparent;"
                    //}
                }
                comp<MudItem> {
                    "xs" => 1
                    "sm" => 1
                    comp<MudMenu> {
                        "Icon" => Icons.Material.Filled.Menu
                        "IconSize" => Size.Large
                        "TransformOrigin" => Origin.TopRight
                        "Class" => "mt-1"
                        concat {  
                            comp<MudMenuItem> {
                                "Icon" => Icons.Material.Filled.Add
                                "Color" => Color.Tertiary
                                Init.createMenu model dispatch
                            }                            
                            // comp<MudMenuItem> {
                            //     "Icon" => Icons.Material.Filled.Save
                            //     on.click(fun _ -> dispatch Ia_Local_Save)
                            //     attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch (Ia_Local_Save))
                            //     "Save chats to local browser storage"
                            // }
                            comp<MudMenuItem> {
                                "Icon" => if model.darkTheme then Icons.Material.Outlined.WbSunny else Icons.Material.Outlined.Nightlight
                                on.click(fun _ -> dispatch ToggleTheme)
                                attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch (ToggleTheme))
                                "Toggle theme"
                            }
                            comp<MudMenuItem> {
                                "Icon" => Icons.Material.Outlined.DeleteSweep
                                on.click(fun _ -> dispatch Ia_ClearChats)
                                attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch (Ia_ClearChats))
                                "Clear Chats"
                            }
                            // comp<MudMenuItem> {
                            //     "Icon" => Icons.Material.Outlined.DeleteForever
                            //     on.click (fun _ -> dispatch Ia_Local_Delete)
                            //     attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch (Ia_Local_Delete))
                            //     "Delete all saved chats from browser storage"
                            // }
                            comp<MudMenuItem> {
                                "Icon" => Icons.Material.Outlined.FolderDelete
                                "IconColor" => Color.Warning
                                on.click (fun _ -> dispatch PurgeLocalData)
                                attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch (PurgeLocalData))
                                "Purge all data stored in local browser storage"
                            }
                            if model.appConfig.EnabledBackends |> List.contains OpenAI then 
                                comp<MudMenuItem> {
                                    "Icon" => Icons.Material.Outlined.Settings
                                    on.click(fun _ -> dispatch (OpenCloseSettings C.MAIN_SETTINGS))
                                    attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch (OpenCloseSettings C.MAIN_SETTINGS))
                                    "Application Settings"
                                }
                        }
                    }
                }
            }
            ecomp<MainSettingsView,_,_> model dispatch {attr.empty()}
        }
