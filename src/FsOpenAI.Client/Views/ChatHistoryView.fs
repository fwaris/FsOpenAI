namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client

type ChatHistoryView() =
    inherit ElmishComponent<Chat,Message>()

    let iconType (c:ChatMessage)  = if c.Role = ChatRole.User then Icons.Material.Filled.Person else Icons.Material.Filled.Assistant

    let icon (c:ChatMessage) =
        comp<MudIcon> {
            //"Align" => if c.Role = ChatRole.User then Align.Start else Align.End
            "Style" => "padding-right:10px;"
            "Icon" => iconType c
            "Size" => Size.Medium
        }

    let color (c:ChatRole) = if c = ChatRole.User then Colors.BlueGrey.Darken2 else Colors.BlueGrey.Darken4

    let text (c:ChatMessage) =  
        comp<MudText> {
            "Typo" => Typo.body1; 
            //"Align" => if c.Role = ChatRole.User then Align.Start else Align.End
            text c.Message 
        }

    let padding (c:ChatMessage) = if c.Role = ChatRole.User then "margin-right:20px;" else "margin-left:20px"
                                        
    override this.View chat dispatch =
        comp<MudList> {
            attr.fragment "ChildContent" (
                concat {
                    for m in chat.Messages do
                        yield
                            comp<MudListItem> { 
                                comp<MudCard> {
                                    "Style" => padding m
                                    "Outlined" => true
                                    //comp<MudCardHeader> {
                                    //}
                                    comp<MudCardContent> {
                                        "Class" => "d-flex"
                                        "Style" => $"background:{color m.Role}"
                                        comp<MudStack> {
                                            "Class" => "flex-grow-1"
                                            "Row" => true
                                            concat {
                                                icon m
                                                text m
                                            }
                                            comp<MudSpacer> {attr.empty()}
                                            comp<MudIconButton> {
                                                //"Class" => "flex-auto align-self-end"
                                                "Icon" => Icons.Material.Filled.Delete
                                                "Size" => Size.Small
                                                on.click(fun e -> dispatch (Chat_DeleteMsg (chat.Id,m)))
                                            }                                        
                                        }
                                    }
                                }
                            }
                }                
            )
        }
