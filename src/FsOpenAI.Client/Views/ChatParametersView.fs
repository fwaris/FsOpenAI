namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client

type ChatParametersView() =
    inherit ElmishComponent<bool*Interaction,Message>()    
    override this.View mdl (dispatch:Message -> unit) =
        let settingsOpen,chat = mdl
        concat {
            comp<MudIconButton> {
                "Icon" => Icons.Material.Outlined.Settings
                on.click(fun e -> dispatch (OpenCloseSettings chat.Id))
            }
            comp<MudPopover> {
                    "Style" => "width:300px"
                    "AnchorOrigin" => Origin.TopLeft
                    "TransformOrigin" => Origin.TopLeft
                    "Open" => settingsOpen
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
                                    on.click (fun e -> dispatch (OpenCloseSettings chat.Id))
                                }
                            }
                            comp<MudSlider<float>> {
                                "Class" => "px-4"
                                "Min" => 0.
                                "Max" => 2.
                                "Step" => 0.1
                                "ValueLabel" => true
                                "Value" => chat.Parameters.Temperature
                                on.change (fun e -> dispatch (Ia_UpdateParms (chat.Id,{chat.Parameters with Temperature = (e.Value :?> string |> float)})))
                                text $"Temperature: {chat.Parameters.Temperature}"
                            }
                            comp<MudSlider<int>> {
                                "Class" => "px-4"
                                "Min" => 600
                                "Max" => 5000
                                "Step" => 300
                                "ValueLabel" => true
                                "Value" => chat.Parameters.MaxTokens
                                on.change (fun e -> dispatch (Ia_UpdateParms (chat.Id,{chat.Parameters with MaxTokens = (e.Value :?> string |> int)})))
                                text $"Max Tokens: {chat.Parameters.MaxTokens}"
                            }
                            comp<MudSlider<float>> {
                                "Class" => "px-4"
                                "Min" => -2.0
                                "Max" => 2.0
                                "Step" => 0.1
                                "ValueLabel" => true
                                "Value" => chat.Parameters.PresencePenalty
                                on.change (fun e -> dispatch (Ia_UpdateParms (chat.Id,{chat.Parameters with PresencePenalty = (e.Value :?> string |> float)})))
                                text $"Presence Penalty: {chat.Parameters.PresencePenalty}"
                            }
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
