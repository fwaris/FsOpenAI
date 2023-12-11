namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components.Web

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

    let border (c:InteractionMessage) = if c.IsUser then "mud-border-primary" else "mud-border-warning"

    override this.View model dispatch =
        let isBusy,chat,msg = model
        let docs = match msg.Role with Assistant r -> r.Docs | _ -> []
        let canSend = Interactions.Interaction.canSubmit chat        
        comp<MudPaper> {
            "Class" => $"d-flex border-solid border flex-column {border msg} rounded-lg pa-1"
            "Style" => $"{padding msg}"
            //"Style" => $"{padding msg} border-solid border-5 mud-border-primary"
            "Elevation" => 0
            comp<MudPaper> {
                "Class" => "d-flex flex-row "
                "Elevation" => 0
                comp<MudPaper> {
                    "Class" => "d-flex flex-grow-1 ma-1"  
                    "Elevation" => 0
                    concat {
                        icon msg
                        if msg.IsOpen then 
                            comp<MudTextField<string>> {
                                "Label" => "Question"
                                "Lines" => 3
                                "Placeholder" => "Enter prompt or question"
                                "Text" => msg.Message
                                attr.callback "OnBlur" (fun (e:FocusEventArgs) -> 
                                    lastMsgRef.Value
                                    |> Option.iter(fun m -> dispatch (Ia_UpdateLastMsg (chat.Id,m.Text))))
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
                    "Class" => "d-flex flex-none align-start ma-1"
                    "Elevation" => 0
                    if not msg.IsOpen then
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Filled.Delete
                            "Size" => Size.Small
                            "Disabled" => chat.IsBuffering 
                            on.click(fun e -> dispatch (Ia_DeleteMsg (chat.Id,msg)))
                        }   
                    if msg.IsOpen then
                        comp<MudIconButton> {
                            "Class" => "mt-1"
                            "Icon" => Icons.Material.Filled.Send
                            "Disabled" => (isBusy || not canSend)
                            "Size" => Size.Small
                            on.click(fun e -> 
                                lastMsgRef.Value
                                |> Option.iter(fun m -> dispatch (Ia_Submit (chat.Id,m.Text))))
                        }                  
                }                                                                                                 
            }
            if not docs.IsEmpty && not chat.IsBuffering then
                comp<MudPaper> {
                    "Class" => "d-flex flex-grow-1 pa-4 mud-text-secondary"
                    "Elevation" => 2
                    comp<MudCarousel<Document>> {
                        "Class" => "d-flex flex-grow-1 align-self-stretch overflow-x-visible"                        
                        "ShowBullets" => false
                        "ArrowsPosition" => Position.End
                        "PreviewNextItem" => true
                        for d in docs do
                            comp<MudCarouselItem> {                            
                                "Class" => "d-flex flex-grow-1 align-self-stretch ml-8 mr-8"
                                comp<MudLink> { 
                                    "Class" => "d-flex flex-grow-1 align-self-center"
                                    "Href" => d.Ref
                                    d.Title
                                }
                            }
                    }
                } 
        }

