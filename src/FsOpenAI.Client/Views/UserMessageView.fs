namespace FsOpenAI.Client.Views
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared

module UserMessage = 
    open Radzen
    open Radzen.Blazor

    let view (m:InteractionMessage) (chat:Interaction) model dispatch = 
        let bg = "background-color: transparent;"
        comp<RadzenRow> {                                        
            "Style" => bg
            attr.``class`` "rz-mt-1 rz-mr-2"
            comp<RadzenColumn> {
                "Size" => 12
                attr.style "display: flex; justify-content: flex-end;"
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    attr.``class`` $"rz-border-radius-5 rz-p-8; rz-background-color-info-lighter"
                    div {
                        attr.style "white-space: pre-line;"
                        attr.``class`` "rz-p-2"
                        text m.Message
                    }
                    comp<RadzenMenu> {
                        "Style" => bg
                        "Responsive" => false
                        comp<RadzenMenuItem> {
                            "Icon" => "refresh"
                            attr.title "Edit and resubmit this message"
                            attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch (Ia_Restart (chat.Id, m)))
                        }
                    }                
                }
            }
        }
    
