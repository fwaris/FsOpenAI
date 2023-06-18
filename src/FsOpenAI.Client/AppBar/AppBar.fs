module AppBar
open Bolero.Html
open MudBlazor
open FsOpenAI.Client.Model

let appBar model dispatch = 
    comp<MudAppBar> {
        "Style" => $"background:{Colors.BlueGrey.Darken3};"
        "Fixed" => true
        "Dense" => true
        comp<MudGrid> {
            comp<MudItem> {
                "xs" => 2
                comp<MudMenu> {
                    "Icon" => Icons.Material.Filled.Settings
                    comp<MudMenuItem> {
                        
                        comp<MudPaper> {
                            comp<MudForm> {
                                concat {
                                    comp<MudTextField<string>> {
                                        text "pass"
                                    }
                                }
                            }
                        }
                    }
                    comp<MudMenuItem> {
                        "Icon" => Icons.Material.Filled.Call
                        on.click (fun ev -> let x = AppSettingsView.dnld "http://google.com" |> Async.RunSynchronously in printfn "%A" x)
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
                    "Size" => if model.highlight_busy then Size.Large else Size.Medium
                }
            }
        }
    }
