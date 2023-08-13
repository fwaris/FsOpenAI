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

    let padding (c:InteractionMessage) = if c.IsUser then "margin-right:20px;" else "margin-left:20px"

    let color (c:InteractionMessage) = if c.IsUser then Colors.BlueGrey.Darken2 else Colors.BlueGrey.Darken4

    let border (c:InteractionMessage) = if c.IsUser then "mud-border-primary" else "mud-border-secondary"

    override this.View model dispatch =
        let isBusy,chat,msg = model
        comp<MudPaper> {
            "Class" => $"d-flex border-solid border {border msg} rounded-lg"
            "Style" => $"{padding msg}"
            //"Style" => $"{padding msg} border-solid border-5 mud-border-primary"
            "Elevation" => 0
            comp<MudPaper> {
                "Class" => "d-flex flex-grow-1 ma-2 pa-2"  
                concat {
                    icon msg
                    if msg.IsOpen then 
                        comp<MudTextField<string>> {
                            "Label" => "Question"
                            "Lines" => 3
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
                "Class" => "d-flex flex-none align-start ma-2 pa-2"
                if not chat.IsBuffering && not msg.IsOpen then
                    comp<MudIconButton> {
                        "Icon" => Icons.Material.Filled.Delete
                        "Size" => Size.Small
                        on.click(fun e -> dispatch (Ia_DeleteMsg (chat.Id,msg)))
                    }   
                if msg.IsOpen then
                    comp<MudIconButton> {
                        "Class" => "mt-4"
                        "Icon" => Icons.Material.Filled.Send
                        "Disabled" => isBusy
                        "Size" => Size.Small
                        on.click(fun e -> 
                            lastMsgRef.Value
                            |> Option.iter(fun m -> dispatch (SubmitInteraction (chat.Id,m.Text))))
                    }                  
            }                                                                                                 
        }

