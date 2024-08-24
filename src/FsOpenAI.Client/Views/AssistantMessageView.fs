namespace FsOpenAI.Client.Views
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared

module AssistantMessage = 
    open Radzen
    open Radzen.Blazor

    let view (msg:InteractionMessage) (chat:Interaction) lastMsg model dispatch =
        let docs = match msg.Role with Assistant r -> r.Docs | _ -> []
        comp<RadzenCard> {
            attr.``class`` $"rz-mt-1 rz-border-radius-3"
            comp<RadzenRow> {
                comp<RadzenColumn> {
                    "Size" => 1
                    comp<RadzenIcon> {
                        "Icon" => "robot_2" // C.DFLT_ASST_ICON
                        "IconStyle" => IconStyle.Info
                    }
                }
                comp<RadzenColumn> {
                    "Size" => 11
                    div {
                        attr.style "white-space: pre-line;"
                        if Utils.isEmpty msg.Message then "..." else msg.Message
                    }
                    table {
                        attr.style "width: 100%;"
                        if not docs.IsEmpty && not chat.IsBuffering then
                            tr {
                                attr.``class`` "rz-mt-1"
                                attr.style "rz-color-on-primary-darker rz-background-color-primary-darker"
                                td {
                                    attr.style "width: 1.5rem;"
                                    ecomp<SearchResultsView,_,_> (Some chat.Id,docs) dispatch { attr.empty() }
                                }
                                td {
                                    comp<RadzenStack> {
                                        attr.``class`` "rz-p-2"
                                        "Style" => "height: 3.5rem; overflow: auto;"
                                        "Orientation" => Orientation.Horizontal
                                        "Wrap" => FlexWrap.Wrap
                                        for d in docs do
                                            comp<RadzenLink> {
                                                attr.title (Utils.shorten 40 d.Text)
                                                attr.``class`` "rz-ml-2"                                                
                                                "Style" => "max-width: 140px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;"
                                                "Path" => d.Ref
                                                "Target" => "_blank"
                                                d.Title
                                            }
                                    }
                                }
                            }
                        if lastMsg && not chat.IsBuffering then
                            tr {
                                td {
                                    attr.colspan 2
                                    match chat.Feedback with
                                    | Some fb -> 
                                        ecomp<FeedbackView,_,_> (fb,chat,model) dispatch { attr.empty() }   
                                    | None -> ()
                                }
                            }
                        }
                    }                                
            }
        }

