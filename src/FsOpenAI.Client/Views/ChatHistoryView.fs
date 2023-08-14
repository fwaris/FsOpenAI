namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop

type ChatHistoryView() =
    inherit ElmishComponent<Interaction*Model,Message>()

    let marker = HtmlRef()

    member val IsBuffering = false with get, set
    member val markerId = "" with get, set

    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set

    member this.ScrollToEnd() =
        if Utils.notEmpty this.markerId then
            this.JSRuntime.InvokeVoidAsync ("scrollTo", [|this.markerId|]) |> ignore

    override this.OnAfterRenderAsync(a) =
        if this.IsBuffering then this.ScrollToEnd()
        base.OnAfterRenderAsync(a)


    override this.View model dispatch =
        let chat,mdl = model
        if chat.IsBuffering then
            this.markerId <- $"{chat.Id}_marker"        
            this.IsBuffering <- true

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
                                    attr.id this.markerId      
                                    attr.style $"color:{Colors.Blue.Default};"
                                    ul {
                                        attr.style "list-style-type: square;"
                                        for t in chat.Notifications do
                                                li {t}                                        
                                        }
                                    }
                                }
                    else
                        yield
                            comp<MudListItem> {
                                "Class" => "d-flex ml-5"
                                div {
                                    attr.id this.markerId      
                                    attr.style $"color:{Colors.Blue.Default};"
                                    "Generating answer ..."
                                }
                            }
                       
            }
        }
