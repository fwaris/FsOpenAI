namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client

type ChatHistoryView() =
    inherit ElmishComponent<Interaction*Model,Message>()

    override this.View model dispatch =
        let chat,mdl = model
        let lastM = List.tryLast chat.Messages
        comp<MudList> {            
            concat {
                for m in chat.Messages do
                    let model = (mdl.busy,chat,m)
                    yield
                        comp<MudListItem> {
                            ecomp<MessageView,_,_> model dispatch {attr.empty()}
                            //comp<MudPaper> {
                            //    //"Style" => $"background:{color m}; {padding m} border-solid border-2 mud-border-primary pa-4"
                            //}
                        }
                if chat.IsBuffering then 
                    if chat.Notifications.IsEmpty |> not then
                        yield
                            comp<MudListItem> { 
                                "Class" => "d-flex ml-5"
                                div {
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
