namespace FsOpenAI.Client.Views
open Bolero.Html
open MudBlazor
open FsOpenAI.Client

module AppBar =

    let appBar model dispatch = 
        comp<MudAppBar> {
            "Style" => $"background:{Colors.BlueGrey.Darken3};"
            "Fixed" => true
            "Dense" => true
            comp<MudGrid> {
                comp<MudItem> {
                    "xs" => 2
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
                comp<MudItem> {
                    "xs" => 8                    
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
            }  
        }    
