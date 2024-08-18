namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop
open FsOpenAI.Shared

module MessageViews = 
    let userMessage (m:InteractionMessage) (chat:Interaction) model dispatch = 
        let bg = "background-color: transparent;"
        comp<RadzenRow> {                                        
            "Style" => bg
            attr.``class`` "rz-mt-1"
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

    let tm = """<svg version="1.1" viewBox="0 0 76.728 91.282" xmlns="http://www.w3.org/2000/svg">
 <g transform="matrix(.2857 0 0 .2857 71.408 28.262)" fill="#e20074">
  <path d="m-33.599 218.73v-22.192h-15.256c-26.315 0-38.393-15.643-38.393-38.665v-232.6h4.5246c49.283 0 80.582 32.707 80.582 80.797v4.3092h18.745v-107.3h-264.58v107.3h18.745v-4.3092c0-48.09 31.298-80.797 80.582-80.797h4.5246v232.6c0 23.022-12.078 38.665-38.393 38.665h-15.256v22.192z"/>
  <path d="m16.603 111.43h-62.914v-63.129h62.914z"/>
  <path d="m-185.07 111.43h-62.914v-63.129h62.914z"/>
 </g>
</svg>"""
    
    let systemMessage (m:InteractionMessage) (chat:Interaction) lastMsg model dispatch =         
        let icon = "assistant"
        let background = "rz-border-danger-dark" 
        let icnstyl = IconStyle.Warning
        comp<RadzenCard> {
            attr.``class`` $"rz-mt-1 rz-border-radius-3"
            comp<RadzenRow> {
                comp<RadzenColumn> {
                    "Size" => 1
                    comp<RadzenIcon> {
                        "Icon" => tm
                        "IconStyle" => icnstyl
                        on.click (fun _ -> dispatch ToggleSideBar)
                    }
                }
                comp<RadzenColumn> {
                    "Size" => 10
                    div {
                        attr.style "white-space: pre-line;"
                        if Utils.isEmpty m.Message then "..." else m.Message
                    }
                }
                comp<RadzenColumn> {
                    "Size" => 1
                    "Style" => "display:flex; flex-direction: column; justify-content: space-between;"
                    comp<RadzenMenu> {
                        "Responsive" => false
                        comp<RadzenMenuItem> {
                            "Disabled" => true
                            "Icon" => ""
                            attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch ToggleSideBar)
                        }
                    }
                    if lastMsg && chat.Feedback.IsSome then
                        comp<RadzenMenu> {
                            "Responsive" => false
                            comp<RadzenMenuItem> {
                                "Icon" => "thumbs_up_down"
                                attr.title "Feedback"
                                attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch ToggleSideBar)
                            }
                        }
                }
            }
        }



type ChatHistoryView() =
    inherit ElmishComponent<Model,Message>()

    let marker = HtmlRef()

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
        comp<RadzenRow> {
            attr.``class`` "rz-mr-1"
            comp<RadzenColumn> { 
                comp<RadzenRow> {
                    "Style" => "max-height: calc(100vh - 17rem);overflow:auto;"
                    comp<RadzenColumn> {
                        match Model.selectedChat model with
                        | Some chat ->                             
                            let lastMsgId = List.tryLast chat.Messages |> Option.map (fun x -> x.MsgId) |> Option.defaultValue ""
                            this.markerId <- $"{chat.Id}_marker"
                            this.IsBuffering <- chat.IsBuffering
                            for m in chat.Messages do
                                if m.IsUser then
                                    yield MessageViews.userMessage m chat model dispatch
                                else
                                    yield MessageViews.systemMessage m chat  (m.MsgId=lastMsgId) model dispatch
                            if chat.IsBuffering then
                                yield 
                                    div {
                                        attr.id this.markerId
                                        concat {
                                            yield ul {attr.empty() }
                                            for t in chat.Notifications do
                                                yield 
                                                    li {                                                        
                                                        comp<RadzenLabel> {
                                                            attr.``class`` "rz-color-info-light"
                                                            "Text" => t
                                                        }
                                                    }
                                        }
                                        div {
                                            attr.id this.markerId
                                            marker
                                            "..."
                                        }
                                    }
                        | None -> ()
                    }
                }
                comp<RadzenRow> {
                    "Style" => "height: auto; margin-right: 1rem; margin-top: 1rem;" 
                    ecomp<QuestionView,_,_> model dispatch {attr.empty()}
                }
            }
        }

(*
type ChatHistoryView() =
    inherit ElmishComponent<Model,Message>()

    let marker = HtmlRef()
    let clist = Ref<MudList<InteractionMessage>>()

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
                comp<MudList<InteractionMessage>> {
                    "Dense" => true
                    clist
                    concat {
                        for m in chat.Messages do
                            let model = (model.busy,chat,m,model)
                            yield
                                comp<MudListItem<InteractionMessage>> {
                                    ecomp<MessageView,_,_> model dispatch {attr.empty()}
                                }
                        if chat.IsBuffering then
                            if chat.Notifications.IsEmpty |> not then
                                yield
                                    comp<MudListItem<string>> {
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
                                    comp<MudListItem<string>> {
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
*)

