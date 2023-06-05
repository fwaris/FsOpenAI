module AppBar
open Bolero.Html
open MudBlazor
open FsOpenAI.Client.Model

let appBar model = 
    comp<MudAppBar> {
        "Style" => $"background:{Colors.BlueGrey.Darken3};"
        "Fixed" => true
        "Dense" => true
        comp<MudGrid> {
            comp<MudItem> {
                "xs" => 2
                comp<MudLink> {
                    "Href" => "http://openai.com"  
                    comp<MudImage> {
                        "Style" => $"background:{Colors.Grey.Lighten3};padding;20px"
                        "Src" => "imgs/icon.jpg"
                        "Width" => 40
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
                "xs" => 2
                "Class" => "d-flex justify-center align-content-center flex-grow-0"
                comp<MudIcon> {
                    "Class" => "align-self-center"
                    "Icon" => if model.busy then Icons.Material.Outlined.DoNotDisturbOnTotalSilence else Icons.Material.Outlined.Circle
                }
            }
        }
    }
