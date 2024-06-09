namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop
open FsOpenAI.Shared

type ChatHistoryView() =
    inherit ElmishComponent<Model,Message>()

    let marker = HtmlRef()
    let clist = Ref<MudList>()

    member val IsBuffering = false with get, set
    member val markerId = "" with get, set

    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set

    member this.CopyToClipboard(text:string) =
        this.JSRuntime.InvokeVoidAsync ("navigator.clipboard.writeText", text) |> ignore

    member this.ScrollToEnd() =
        if Utils.notEmpty this.markerId then
            this.JSRuntime.InvokeVoidAsync ("fso_scrollTo", [|this.markerId|]) |> ignore

    override this.OnAfterRenderAsync(a) =
        if this.IsBuffering then this.ScrollToEnd()
        base.OnAfterRenderAsync(a)


    override this.View model dispatch =
        let chat = Model.selectedChat model
        let botPad =
            chat
            |> Option.map (fun c -> match c.InteractionType with IndexQnADoc _ -> 29 | _ -> 22)
            |> Option.defaultValue 22
        chat
        |> Option.map (fun chat ->
            if chat.IsBuffering then
                this.markerId <- $"{chat.Id}_marker"
                this.IsBuffering <- true
            comp<MudPaper> {
                "Class" => "overflow-auto"
                "Style" => $"height: 100vh; padding-bottom: {botPad}rem;"
                comp<MudList> {
                    "Dense" => true
                    clist
                    concat {
                        for m in chat.Messages do
                            let model = (model.busy,chat,m,model)
                            yield
                                comp<MudListItem> {
                                    ecomp<MessageView,_,_> model dispatch {attr.empty()}
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
            })
        |> Option.defaultValue (div {text ""})

