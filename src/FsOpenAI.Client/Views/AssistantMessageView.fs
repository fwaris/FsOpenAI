namespace FsOpenAI.Client.Views
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared
open FSharp.Formatting.Markdown

module AssistantMessage =
    open Radzen
    open Radzen.Blazor

    let view (msg:InteractionMessage) (chat:Interaction) lastMsg model dispatch =
        let docs = match msg.Role with Assistant r -> r.DocRefs | _ -> []
        let docsr =
            if docs |> List.exists (fun x -> x.SortOrder.IsSome) then
                docs |> List.filter (fun x -> x.SortOrder.IsSome)
            else
                docs
        comp<RadzenCard> {
            attr.``class`` $"rz-mt-1 rz-border-radius-3"
            comp<RadzenRow> {
                comp<RadzenColumn> {
                    "Size" => 1
                    match model.appConfig.AssistantIcon with //assume  alt icon is an image (updated to match radzen changes)
                    | Some path ->
                        comp<RadzenImage> {
                            "Path" => path
                            "Style" => "width: 1.5rem; height: 1.5rem;"
                        }
                    | None ->
                        comp<RadzenIcon> {
                            "Icon" =>   C.DFLT_ASST_ICON
                            "IconColor" => (model.appConfig.AssistantIconColor |> Option.defaultValue  C.DFLT_ASST_ICON_COLOR)
                        }
                }
                comp<RadzenColumn> {
                    "Size" => 11
                    div {
                        attr.style "white-space: pre-line;"
                        if Utils.isEmpty msg.Message then
                            "..."
                        else
                            let html = Markdown.ToHtml(Markdown.Parse(msg.Message))
                            let html = html
                                            .Replace("<p>", "")
                                            .Replace("</p>", "<br>")

                            Bolero.Html.rawHtml(html)
                    }
                    table {
                        attr.style "width: 100%;"
                        if not docs.IsEmpty && not chat.IsBuffering then
                            tr {
                                attr.``class`` "rz-mt-1"// rz-color-on-primary-darker rz-background-color-primary-darker"
                                attr.style "background-color: var(--rz-primary-lighter);"
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
                                        for d in docsr do
                                            comp<RadzenLink> {
                                                attr.title (Utils.shorten 40 d.Title)
                                                attr.``class`` "rz-ml-2 "
                                                "Style" => "max-width: 140px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; color: var(--rz-danger-light);"
                                                "Path" => d.Ref
                                                "Target" => "_blank"
                                                $"{d.Id}: {d.Title}"
                                            }
                                    }
                                }
                            }
                        if lastMsg && not chat.IsBuffering then
                            match Interactions.CodeEval.Interaction.codeBag chat with
                            | None -> ()
                            | Some v ->
                                tr {
                                    td {
                                        attr.colspan 2
                                        ecomp<CodeAndPlanView,_,_> model dispatch {attr.empty()}
                                    }
                                }
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

