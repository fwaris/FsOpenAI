namespace FsOpenAI.Client.Views
open System
open Bolero.Html
open MudBlazor
open FsOpenAI.Client

module AppBar =
    let createMenuGroup group dispatch =
        concat {
            for (icon,name,createType) in group do 
                comp<MudMenuItem> {
                    "Icon" => icon
                    on.click(fun _ -> dispatch (Ia_Add createType))
                    text name
                }
        }        

    let createMenu model dispatch =
        let groups = 
            model.interactionCreateTypes 
            |> List.groupBy (fun (_,_,c) -> match c with CreateChat bkend -> bkend | CreateQA bkend -> bkend)
            |> List.map snd
      
        let rec loop (acc:Bolero.Node) gs =
            match gs with 
            | [] -> acc
            | g1::rest -> 
                let n1 = createMenuGroup g1 dispatch
                concat {
                    yield acc
                    yield comp<MudDivider> {"DividerType" => DividerType.Middle}                        
                    yield n1 
                }
        let g1,rest = groups.Head,groups.Tail
        loop (createMenuGroup g1 dispatch) rest

    let appBar model dispatch = 
        comp<MudAppBar> {
            "Style" => $"background:{if model.darkTheme then Colors.BlueGrey.Darken3 else Colors.BlueGrey.Lighten1};"
            "Fixed" => true
            "Dense" => true
            comp<MudGrid> {
                comp<MudItem> {
                    "xs" => 5                    
                    //"Class" => "d-flex justify-center align-content-center flex-grow-1"
                    comp<MudLink> {
                        "Href" => "https://github.com/fwaris/FsOpenAI"
                        "Target" => "_blank"
                        comp<MudImage> {
                            "Class" => "mt-2"
                            "ObectFit" => ObjectFit.ScaleDown
                            //"Height" => Nullable 40
                            "Width" => Nullable 160
                            "Src" => "imgs/logo.png"
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
                            "Color" => Color.Tertiary
                            createMenu model dispatch
                        }                    
                    }
                }
                comp<MudItem> {
                    "xs" => 1
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
                    "xs" => 1
                    comp<MudIconButton> {
                        "Class" => "mt-1"
                        "Icon" => if model.darkTheme then Icons.Material.Filled.WbSunny else Icons.Material.Outlined.WbSunny
                        on.click (fun _ -> dispatch ToggleTheme)
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
            }
        }    
