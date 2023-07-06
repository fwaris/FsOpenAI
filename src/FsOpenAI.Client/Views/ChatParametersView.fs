namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client

type ChatParametersView() =
    inherit ElmishComponent<Chat,Message>()
    
    override this.View chat (dispatch:Message -> unit) =
        comp<MudPopover> {
                "Style" => "width:300px"
                "AnchorOrigin" => Origin.BottomLeft
                "TransformOrigin" => Origin.BottomLeft
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
                            "Max" => 2.
                            "Step" => 0.1
                            "ValueLabel" => true
                            "Value" => chat.Parameters.Temperature
                            on.change (fun e -> dispatch (Chat_UpdateParms (chat.Id,{chat.Parameters with Temperature = (e.Value :?> string |> float)})))
                            text $"Temperature: {chat.Parameters.Temperature}"
                        }
                        comp<MudSlider<int>> {
                            "Class" => "px-4"
                            "Min" => 600
                            "Max" => 5000
                            "Step" => 300
                            "ValueLabel" => true
                            "Value" => chat.Parameters.MaxTokens
                            on.change (fun e -> dispatch (Chat_UpdateParms (chat.Id,{chat.Parameters with MaxTokens = (e.Value :?> string |> int)})))
                            text $"Max Tokens: {chat.Parameters.MaxTokens}"
                        }
                        comp<MudSlider<float>> {
                            "Class" => "px-4"
                            "Min" => -2.0
                            "Max" => 2.0
                            "Step" => 0.1
                            "ValueLabel" => true
                            "Value" => chat.Parameters.PresencePenalty
                            on.change (fun e -> dispatch (Chat_UpdateParms (chat.Id,{chat.Parameters with PresencePenalty = (e.Value :?> string |> float)})))
                            text $"Presence Penalty: {chat.Parameters.PresencePenalty}"
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
