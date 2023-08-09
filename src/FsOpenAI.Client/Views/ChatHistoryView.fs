namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client

type ChatHistoryView() =
    inherit ElmishComponent<Interaction*Model,Message>()

    let padding (c:InteractionMessage) = if c.IsUser then "margin-right:20px;" else "margin-left:20px"

    let color (c:InteractionMessage) = if c.IsUser then Colors.BlueGrey.Darken2 else Colors.BlueGrey.Darken4

    override this.View model dispatch =
        let chat,mdl = model
        let lastM = List.tryLast chat.Messages
        comp<MudList> {            
            concat {
                for m in chat.Messages do
                    let model = (mdl.busy,chat,m)
                    yield
                        comp<MudListItem> { 
                            comp<MudPaper> {
                                "Class" => $"d-flex"
                                "Style" => $"background:{color m}; {padding m}"                                    
                                ecomp<MessageView,_,_> model dispatch {attr.empty()}
                            }
                        }
                if chat.IsBuffering then 
                    if chat.Notifications.IsEmpty |> not then
                        yield
                            comp<MudListItem> { 
                                div {
                                    attr.``class`` "d-flex"
                                    attr.style $"color:{Colors.Blue.Default};"
                                    ul {
                                        attr.style "list-style-type: square;"
                                        for t in chat.Notifications do
                                                li {t}                                        
                                        }
                                    }
                                }                            
            }                            
        }
