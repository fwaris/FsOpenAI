module Chat
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client.Model
open Azure.AI.OpenAI

type ChatView() =
    inherit ElmishComponent<Model,Message>()

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
            text c.Content 
        }

    let padding (c:ChatMessage) = if c.Role = ChatRole.User then "margin-right:20px;" else "margin-left:20px"
                                        
    override this.View model dispatch =
        comp<MudList> {
            attr.fragment "ChildContent" (
                concat {
                    for c in model.chat do
                        yield
                            comp<MudListItem> { 
                                comp<MudCard> {
                                    "Style" => padding c
                                    "Outlined" => true
                                    //comp<MudCardHeader> {
                                    //}
                                    comp<MudCardContent> {
                                        "Class" => "d-flex"
                                        "Style" => $"background:{color c.Role}"
                                        icon c                                        
                                        text c
                                    }
                                    comp<MudCardActions> {
                                        "Class" => "p-1 align-content-end justify-end"                                        
                                        comp<MudIconButton> {
                                            "Icon" => Icons.Material.Filled.Delete
                                            "Size" => Size.Small
                                            on.click(fun e -> dispatch (DeleteChatItem c))
                                        }                                        
                                    }
                                }

                            }
                }                
            )
        }

        //comp<MudTimeline> {
        //    //"Class" => "mud-width-full mud-height-full"            
        //    attr.fragment "ChildContent" (
        //        concat {
        //            for c in model.chat do
        //                yield
        //                    comp<MudTimelineItem> { 
        //                        "Size" => Size.Large
        //                        "Variant" => Variant.Outlined
        //                        attr.fragment "ItemDot" (comp<MudIcon> {"Size" => Size.Large; "Icon" => icon c})
        //                        text c
        //                    }
        //        }
        //    )
        //}


