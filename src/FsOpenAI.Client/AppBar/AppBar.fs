namespace FsOpenAI.Client.Views
open Bolero.Html
open MudBlazor
open FsOpenAI.Client

module AppBar =

    let appBar model dispatch = 
        comp<MudAppBar> {
            "Style" => $"background:{if model.darkTheme then Colors.BlueGrey.Darken3 else Colors.BlueGrey.Lighten1};"
            "Fixed" => true
            "Dense" => true
            comp<MudGrid> {
                comp<MudItem> {
                    "xs" => 1
                    comp<MudTooltip> {
                        "Text" => "Save chats to local browser storage"
                        "Arrow" => true
                        "Placement" => Placement.Right
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Filled.Save
                            "Class" => "mt-1"
                            //"Size" => Size.Large
                            on.click (fun _ -> dispatch Ia_Save)
                        }                    
                    }
                }
                comp<MudItem> {
                    "xs" => 1                   
                    comp<MudTooltip> {
                        "Text" => "New chat tab"
                        "Arrow" => true
                        comp<MudMenu> {
                            "Icon" => Icons.Material.Filled.Add
                            "Size" => Size.Large
                            concat {
                                for (icon,name,createType) in model.interactionCreateTypes do 
                                    comp<MudMenuItem> {
                                        "Icon" => icon
                                        on.click(fun _ -> dispatch (Ia_Add createType))
                                        text name
                                    }
                            }
                        }                    
                    }
                }
                comp<MudItem> {
                    "xs" => 1
                    comp<MudIconButton> {
                        "Class" => "mt-1"
                        "Icon" => if model.darkTheme then Icons.Material.Filled.WbSunny else Icons.Material.Outlined.WbSunny
                        on.click (fun _ -> dispatch ToggleTheme)
                    }
                }
                comp<MudItem> {
                    "xs" => 5                    
                    "Class" => "d-flex justify-center align-content-center flex-grow-1"
                    comp<MudText> {
                        "Type" => Typo.h2
                        "Class" => "align-self-center"
                        text "Azure OpenAI Chat"
                    }
                }
                comp<MudItem> {
                    "xs" => 1
                    //"Class" => "d-flex justify-center align-content-center flex-grow-0"
                    concat {
                        if model.busy then 
                            comp<MudProgressCircular> {
                                "Class" => "mt-4"
                                "Color" => Color.Secondary
                                "Indeterminate" => true
                                "Size" => if model.highlight_busy then Size.Medium else Size.Small
                            }                            
                    }
                }
                comp<MudItem> {
                    "xs" => 1
                    ecomp<MainSettingsView,_,_> model dispatch {attr.empty()}
                }
                comp<MudItem> {
                    "xs" => 1
                    comp<MudMenu> {
                        "Icon" => Icons.Material.Filled.Menu
                        "Class" => "mt-1"
                        concat {
                            comp<MudMenuItem> {
                                on.click(fun _ -> dispatch Ia_ClearChats)
                                "Remove all chats tabs"
                            }
                            comp<MudMenuItem> {
                                on.click (fun _ -> dispatch Ia_DeleteSavedChats)
                                "Delete all saved chats from browser storage"
                            }
                        }
                    }
                }
            }  
        }    
