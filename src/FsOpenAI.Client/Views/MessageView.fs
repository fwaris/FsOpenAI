namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open Microsoft.JSInterop
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components

type MessageView() =
    inherit ElmishComponent<bool*Interaction*InteractionMessage,Message>()

    let lastMsgRef = Ref<MudTextField<string>>()

    let iconType (c:InteractionMessage)  = if c.IsUser then Icons.Material.Filled.Person else Icons.Material.Filled.Assistant

    let icon (c:InteractionMessage) =
        comp<MudIcon> {
            //"Align" => if c.Role = ChatRole.User then Align.Start else Align.End
            "Style" => "padding-right:10px;"
            "Icon" => iconType c
            "Size" => Size.Medium
        }

    override this.View model dispatch =
        let isBusy,chat,msg = model
        comp<MudContainer> {
            "Class" => "d-flex flex-grow-1 ma-1 pa-1"
            comp<MudPaper> {
                "Class" => "d-flex"  
                "Width" => "100%"
                concat {
                    icon msg
                    if msg.IsOpen then 
                        comp<MudTextField<string>> {
                            //attr.callback "ValueChanged" (fun e -> dispatch (Chat_UpdateLastMsg (chat.Id,e)))
                            //"Variant" => Variant.Outlined
                            "Label" => "Question"
                            "Lines" => 2
                            "Placeholder" => "Enter prompt or question"
                            "Text" => msg.Message
                            lastMsgRef
                        }
                    else                    
                        comp<MudText> {
                            attr.style "white-space: pre-line;"
                            if String.IsNullOrWhiteSpace msg.Message then "..." else msg.Message
                        }
                }
            }
            comp<MudPaper> {
                "Class" => "d-flex flex-none"
                if not chat.IsBuffering && not msg.IsOpen then
                    comp<MudIconButton> {
                        "Icon" => Icons.Material.Filled.Delete
                        "Size" => Size.Small
                        on.click(fun e -> dispatch (Ia_DeleteMsg (chat.Id,msg)))
                    }   
                if msg.IsOpen then
                    comp<MudIconButton> {
                        "Icon" => Icons.Material.Filled.Send
                        "Disabled" => isBusy
                        "Size" => Size.Small
                        on.click(fun e -> 
                            lastMsgRef.Value
                            |> Option.iter(fun m -> dispatch (SubmitInteraction (chat.Id,m.Text))))
                    }                  
            }                                                                                                 
        }

