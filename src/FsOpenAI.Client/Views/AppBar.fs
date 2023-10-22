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
                            //if model.appConfig.RequireLogin then 
                //    comp<MudItem> {
                //        "xs" => 2
                //        "sm" => 1
            //ecomp<AvatarView,_,_> model dispatch {attr.empty()}
                //    }

            comp<MudGrid> {
                comp<MudItem> {
                    "xs" => 1
                    "sm" => 5
                    "Class" => "d-none d-sm-flex"
                    //"Class" => "d-flex justify-center align-content-center flex-grow-1"
                    comp<MudLink> {
                        "Href" => match model.appConfig.LogoUrl with Some x -> x | None -> "#"
                        "Target" => "_blank"
                        comp<MudImage> {
                            "Class" => "mt-2"
                            "ObjectFit" => ObjectFit.ScaleDown
                            //"Height" => Nullable 40
                            "Width" => Nullable 160
                            "Src" => "imgs/logo.png"
                        }
                    }
                }
                comp<MudItem> {
                    "xs" => 2                   
                    "sm" => 1
                    comp<MudTooltip> {
                        "Text" => "New chat tab"
                        "Arrow" => true
                        "Delay" => 100.
                        comp<MudMenu> {
                            "Icon" => Icons.Material.Filled.Add
                            "Size" => Size.Large
                            "Color" => Color.Tertiary
                            createMenu model dispatch
                        }                    
                    }
                }
                comp<MudItem> {
                    "xs" => 2
                    "sm" => 1
                    comp<MudTooltip> {
                        "Text" => "Save chats to local browser storage"
                        "Arrow" => true
                        "Placement" => Placement.Bottom
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Filled.Save
                            "Class" => "mt-1"
                            //"Size" => Size.Large
                            on.click (fun _ -> dispatch Ia_Save)
                        }                    
                    }
                }
                comp<MudItem> {
                    "xs" => 2
                    "sm" => 1
                    comp<MudIconButton> {
                        "Class" => "mt-1"
                        "Icon" => if model.darkTheme then Icons.Material.Filled.WbSunny else Icons.Material.Outlined.WbSunny
                        on.click (fun _ -> dispatch ToggleTheme)
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
                            if model.appConfig.EnableOpenAI then 
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
            ecomp<MainSettingsView,_,_> model dispatch {attr.empty()}
        }    
