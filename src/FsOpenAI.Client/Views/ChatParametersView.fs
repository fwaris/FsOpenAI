namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client

type ChatParametersView() =
    inherit ElmishComponent<bool*Interaction*Model,Message>()    
    
    override this.View mdl (dispatch:Message -> unit) =
        let settingsOpen,chat,model = mdl

        let chatModels = Interactions.chatModels model.serviceParameters chat 
        let completionsModels = Interactions.completionsModels model.serviceParameters chat
        let embeddingsModels = Interactions.embeddingsModel model.serviceParameters chat

        let dispatchSel id f dispatch (xs:string seq)=
            xs 
            |> Seq.tryHead
            |> Option.iter (fun mdl -> dispatch (Ia_UpdateParms (id, f mdl)))

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
                            comp<MudSelect<string>> {
                                "Class" => "px-4"
                                "Label" => "Chat Model"
                                attr.callback "SelectedValuesChanged" (dispatchSel chat.Id (fun m -> {chat.Parameters with ChatModel=m}) dispatch)
                                "SelectedValues" => (chatModels |> List.filter snd |> List.map fst)
                                for (ch,b) in Interactions.chatModels model.serviceParameters chat do
                                    comp<MudSelectItem<string>> {
                                        "Value" => ch
                                    }
                            }
                            comp<MudSelect<string>> {
                                "Class" => "px-4"
                                "Label" => "Completions Model"
                                attr.callback "SelectedValuesChanged" (dispatchSel chat.Id (fun m -> {chat.Parameters with CompletionsModel=m}) dispatch)
                                "SelectedValues" => (completionsModels |> List.filter snd |> List.map fst)
                                for (ch,b) in Interactions.completionsModels model.serviceParameters chat do
                                    comp<MudSelectItem<string>> {
                                        "Value" => ch
                                    }
                            }
                            comp<MudSelect<string>> {
                                "Class" => "px-4"
                                "Label" => "Embeddings Model"
                                attr.callback "SelectedValuesChanged" (dispatchSel chat.Id (fun m -> {chat.Parameters with EmbeddingsModel=m}) dispatch)
                                "SelectedValues" => (embeddingsModels |> List.filter snd |> List.map fst)
                                for (ch,b) in Interactions.embeddingsModel model.serviceParameters chat do
                                    comp<MudSelectItem<string>> {
                                        "Value" => ch
                                    }
                            }
                            match chat.InteractionType with
                            | QA bag ->
                                comp<MudSlider<int>> {
                                    "Class" => "px-4"
                                    "Min" => 5
                                    "Max" => 30
                                    "Step" => 1
                                    "ValueLabel" => true
                                    "Value" => bag.MaxDocs
                                    on.change (fun e -> dispatch (Ia_UpdateQaBag (chat.Id,{bag with MaxDocs = (e.Value :?> string |> int)})))
                                    text $"Max Documents: {bag.MaxDocs}"
                                }
                            | _ -> ()
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
