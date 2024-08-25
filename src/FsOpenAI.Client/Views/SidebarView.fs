namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared
open Radzen
open Radzen.Blazor
open FsOpenAI.Shared.Interactions
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web

type SidebarView() =
    inherit ElmishComponent<Model,Message>()

    override this.View model dispatch =        
        let selChatId = Model.selectedChat model |> Option.map (fun x -> x.Id) |> Option.defaultValue ""
        comp<RadzenSidebar> {
            "Style" => "background-color: var(--rz-surface);"
            "Expanded" => TmpState.isOpenDef true C.SIDE_BAR_EXPANDED model
            attr.callback "ExpandedChanged" (fun (b:bool) -> dispatch (SidebarExpanded b))
            comp<RadzenColumn> {
                comp<RadzenStack> {
                    "Style" => "width: 100%;"
                    attr.``class`` "rz-p-2"
                    "AlignItems" => AlignItems.Center
                    "Orientation" => Orientation.Horizontal
                    // comp<RadzenText> {
                    //     "Text" => "Chats"
                    //     "TextStyle" => TextStyle.H6
                    // }
                    comp<RadzenButton> {
                        "ButtonStyle" => ButtonStyle.Primary
                        attr.``class`` "rz-border-radius-10 rz-shadow-10"
                        attr.title "Add chat"
                        "Icon" => "add"
                        attr.callback "Click" (fun (e:MouseEventArgs) -> dispatch (Ia_Add InteractionCreateType.Crt_IndexQnA))
                    }
                }
                comp<RadzenStack> {
                    "Gap" => "0"
                    for x in model.interactions do
                        comp<RadzenStack> {
                            "Gap" => "0"
                            attr.``class`` "rz-p-2"
                            "Orientation" => Orientation.Horizontal
                            "AlignItems" => AlignItems.Center
                            comp<RadzenButton> {
                                "Size" => ButtonSize.Small
                                "ButtonStyle" => ButtonStyle.Base
                                if x.Id = selChatId then                            
                                    "Style" => "outline: 2px solid var(--rz-primary); width: 100%;"
                                else 
                                    "Style" => "width: 100%;"
                                "ButtonStyle" => ButtonStyle.Base   

                                attr.callback "Click" (fun (e:MouseEventArgs) -> dispatch (Ia_Selected x.Id))
                                Interaction.label x
                            }                    
                            comp<RadzenButton> {
                                "ButtonStyle" => ButtonStyle.Base
                                attr.``class`` "rz-ml-2"
                                "Style" => "background-color: var(--rz-surface);"
                                
                                "Size" => ButtonSize.Small
                                "Icon" => "close"
                                attr.callback "Click" (fun (e:MouseEventArgs) -> dispatch (Ia_Remove x.Id))
                            }
                        }
                }
            }
        }
