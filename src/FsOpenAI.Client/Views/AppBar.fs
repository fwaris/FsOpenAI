namespace FsOpenAI.Client.Views
open System
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components.Web

module AppBar =

    let appBar model dispatch = 
        comp<MudAppBar> {
            "Style" => $"background:{if model.darkTheme then Colors.BlueGrey.Darken3 else Colors.BlueGrey.Lighten1};"
            "Fixed" => true
            "Dense" => true
            comp<MudGrid> {
                comp<MudItem> {
                    "xs" => 1
                    "sm" => 4
                    "Class" => "d-none d-sm-flex"
                    comp<MudLink> {
                        "Href" => match model.appConfig.LogoUrl with Some x -> x | None -> "#"
                        "Target" => "_blank"
                        comp<MudImage> {
                            "Class" => "mt-2"
                            "ObjectFit" => ObjectFit.ScaleDown
                            //"Height" => Nullable 40
                            "Width" => Nullable 160
                            "Src" => $"app/imgs/logo.png"
                        }
                    }
                }
                comp<MudItem> {
                    "xs" => 2                   
                    "sm" => 1
                    comp<MudMenu> {
                        "Title" => "Add a new chat tab"
                        "Icon" => Icons.Material.Filled.Add
                        "Size" => Size.Large
                        "Color" => Color.Tertiary
                        Init.createMenu model dispatch
                    }                    
                }
                comp<MudItem> {
                    "xs" => 2
                    "sm" => 1
                    comp<MudIconButton> {
                        "Title" => "Save chats to local browser storage"
                        "Icon" => Icons.Material.Filled.Save
                        "Class" => "mt-1"
                        //"Size" => Size.Large
                        on.click (fun _ -> dispatch Ia_Save)
                    }                    
                }
                comp<MudItem> {
                    "xs" => 2
                    "sm" => 1
                    comp<MudIconButton> {
                        "Class" => "mt-1"
                        "Title" => "Toggle theme"
                        "Icon" => if model.darkTheme then Icons.Material.Outlined.WbSunny else Icons.Material.Outlined.Nightlight
                        on.click (fun _ -> dispatch ToggleTheme)
                    }
                }
                comp<MudItem> {
                    "xs" => 2
                    "sm" => 1
                    comp<MudIconButton> {
                        "Class" => "mt-1"
                        "Title" => "Toggle vertical/horizontal tabs"
                        "Icon" => if model.tabsUp then Icons.Material.Outlined.KeyboardDoubleArrowLeft else Icons.Material.Outlined.KeyboardDoubleArrowUp
                        on.click (fun _ -> dispatch ToggleTabs)
                    }
                }
                comp<MudItem> {
                    "xs" => 2
                    "sm" => 1
                    comp<MudMenu> {
                        "Icon" => Icons.Material.Filled.Menu
                        "TransformOrigin" => Origin.TopCenter
                        "Class" => "mt-1"
                        concat {            
                            comp<MudMenuItem> {
                                "Icon" => Icons.Material.Outlined.DeleteSweep
                                on.click(fun _ -> dispatch Ia_ClearChats)
                                attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch (Ia_ClearChats))
                                "Remove all chats tabs"
                            }
                            comp<MudMenuItem> {
                                "Icon" => Icons.Material.Outlined.DeleteForever
                                on.click (fun _ -> dispatch Ia_DeleteSavedChats)
                                attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch (Ia_DeleteSavedChats))
                                "Delete all saved chats from browser storage"
                            }
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
                if model.appConfig.RequireLogin then 
                    comp<MudItem> {
                        "xs" => 2
                        "sm" => 1
                        ecomp<AvatarView,_,_> model dispatch {attr.empty()}
                    }
                comp<MudItem> {
                    "xs" => 2
                    "sm" => 1
                    //"Class" => "d-flex justify-center align-content-center flex-grow-0"
                    concat {
                        if model.busy then 
                            comp<MudProgressCircular> {
                                "Class" => "mt-5"
                                "Color" => Color.Secondary
                                "Indeterminate" => true
                                "Size" => Size.Small
                            }                            
                    }
                }
            }            
            //comp<MudImage> {
            //    "Style" => "position:fixed; right:5px; top:5px; height: 5rem; width: 5rem; object-fit: contain; border-radius:25px"
            //    "Elevation" => 5
            //    "Src" => "imgs/robby.png"
            //}
            ecomp<MainSettingsView,_,_> model dispatch {attr.empty()}
        }    
