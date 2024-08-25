namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions

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
                    comp<RadzenColumn> {
                        "Sytle" => "height: 1rem;"
                        //"Size" => 1
                        match Model.selectedChat model with
                        | Some chat ->
                            let m = 
                                {
                                    ChatId = chat.Id
                                    Model = model
                                    Parms = chat.Parameters
                                    QaBag = Interaction.qaBag chat
                                    SystemMessage = Interaction.systemMessage chat
                                }
                            ecomp<ChatSettingsView,_,_> m dispatch {attr.empty()}
                        | None ->   ()
                    }
                }
                comp<RadzenRow> {
                    "Style" => "max-height: calc(100vh - 19rem);overflow-y:auto;overflow-x:hidden;"
                    comp<RadzenColumn> {
                        match Model.selectedChat model with
                        | Some chat ->                             
                            let lastMsgId = List.tryLast chat.Messages |> Option.map (fun x -> x.MsgId) |> Option.defaultValue ""
                            this.markerId <- $"{chat.Id}_marker"
                            this.IsBuffering <- chat.IsBuffering
                            for m in chat.Messages do
                                if m.IsUser then
                                    yield UserMessage.view m chat model dispatch
                                else
                                    yield AssistantMessage.view m chat  (m.MsgId=lastMsgId) model dispatch
                            if chat.IsBuffering then
                                yield 
                                    comp<RadzenTimeline> {                                
                                        //attr.``class`` "rz-color-on-color-secondary-lighter rz-background-color-secondary-lighter rz-mt-1 rz-p-2"                                
                                        attr.``class`` "rz-mt-1 rz-p-2" 
                                        "LinePosition" => LinePosition.Left
                                        attr.fragment "Items" (
                                            concat {
                                                yield
                                                    comp<RadzenTimelineItem> {
                                                        div {
                                                            attr.id this.markerId
                                                            marker
                                                            "..."
                                                        }
                                                    }
                                                for t in List.rev chat.Notifications do
                                                    yield 
                                                        comp<RadzenTimelineItem>  { text t }
                                            })
                                    }
                        | None -> ()
                    }
                }
                comp<RadzenRow> {
                    "Style" => "height: auto; margin-top: 1rem;" 
                    attr.``class`` "rz-mt-1"
                    ecomp<QuestionView,_,_> model dispatch {attr.empty()}
                }
            }
        }
