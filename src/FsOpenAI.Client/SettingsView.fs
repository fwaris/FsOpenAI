module Settings
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client.Model

type SettingsView() =
    inherit ElmishComponent<Model,Message>()
    
    override this.View model dispatch =
        comp<MudPopover> {
                "Style" => "width:300px"
                "AnchorOrigin" => Origin.BottomLeft
                "TransformOrigin" => Origin.BottomLeft
                "Open" => model.settingsOpen
                comp<MudPaper> {
                    "Outlined" => true
                    "Class" => "py-4"
                    comp<MudStack> {
                        comp<MudStack> {
                            "Row" => true
                            comp<MudText> {
                                "Class" => "px-4"
                                "Typo" => Typo.h6
                                "Settings"
                            }
                            comp<MudSpacer>{
                                attr.empty()
                            }
                            comp<MudIconButton> {
                                "Class" => "align-self-end"
                                "Icon" => Icons.Material.Filled.Close
                                on.click (fun e -> dispatch (OpenCloseSettings false))
                            }
                        }
                        comp<MudSlider<float>> {
                            "Class" => "px-4"
                            "Min" => 0.
                            "Max" => 1.
                            "Step" => 0.1
                            "ValueLabel" => true
                            "Value" => model.temperature
                            on.change (fun e -> dispatch (SetTemperature (e.Value :?> string |> float)))
                            text $"Temperature: {model.temperature}"
                        }
                        comp<MudSlider<int>> {
                            "Class" => "px-4"
                            "Min" => 600
                            "Max" => 3000
                            "Step" => 300
                            "ValueLabel" => true
                            "Value" => model.max_tokens
                            on.change (fun e -> dispatch (SetMaxTokens (e.Value :?> string |> int)))
                            text $"Max Tokens: {model.max_tokens}"
                        }
                        comp<MudSlider<float>> {
                            "Class" => "px-4"
                            "Min" => 0.5
                            "Max" => 1.0
                            "Step" => 0.1
                            "ValueLabel" => true
                            "Value" => model.top_prob
                            on.change (fun e -> dispatch (SetTopProb (e.Value :?> string |> float)))
                            text $"Top Prob.: {model.top_prob}"
                        }
                    }
                }
            }
(*
  engine="gpt-35-turbo",
  messages = [],
  temperature=0.7,
  max_tokens=800,
  top_p=0.95,
  frequency_penalty=0,
  presence_penalty=0,
  stop=None)
*)
