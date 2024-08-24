namespace FsOpenAI.Client.Views
open System
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared

type FeedbackViewB() =
    inherit ElmishComponent<Feedback*Interaction*Model,Message>()
    let commentRef = Ref<MudTextField<string>>()
    override this.View m dispatch =
        let fb,chat,model = m
        comp<MudPopover> {
            "Style" => "width:80%;max-height:500px;max-width:500px;"
            "Class" => "d-flex flex-grow-1"
            "AnchorOrigin" => Origin.BottomCenter
            "TransformOrigin" => Origin.BottomCenter
            "Elevation" => 6
            "Open" => (TmpState.isFeedbackOpen chat.Id model)
            "Paper" => true
            let colorUp = if fb.ThumbsUpDn > 0 then Color.Success else Color.Default
            let colorDn = if fb.ThumbsUpDn < 0 then Color.Error else Color.Default
            comp<MudTextField<string>> {
                "Class" => "d-flex flex-grow-1 mt-2 ml-2 mb-2"
                "Placeholder" => "Comment (optional)"
                "Lines" => 3
                "Variant" => Variant.Filled
                "Text" => (fb.Comment |> Option.defaultValue "")
                attr.callback "OnBlur" (fun (e:FocusEventArgs) ->
                    commentRef.Value
                    |> Option.iter(fun m ->
                        dispatch(Ia_Feedback_Set (chat.Id, {fb with Comment = Some m.Text}))))
                commentRef
            }
            comp<MudPaper> {
                "Class" => "ma-2 align-self-center"
                comp<MudIconButton> {
                    "Icon" => Icons.Material.Outlined.ThumbUp
                    "Color" => colorUp
                    on.click (fun _ -> dispatch (Ia_Feedback_Set(chat.Id, {fb with ThumbsUpDn = if fb.ThumbsUpDn > 0 then 0 else +1})))
                }
                comp<MudIconButton> {
                    "Icon" => Icons.Material.Outlined.ThumbDown
                    "Color" => colorDn
                    on.click (fun _ -> dispatch (Ia_Feedback_Set(chat.Id, {fb with ThumbsUpDn = if fb.ThumbsUpDn < 0 then 0 else -1})))
                }
            }
            comp<MudButton> {
                "Class" => "align-self-center"
                "Variant" => Variant.Filled
                "Color" => Color.Primary
                on.click (fun _ ->
                    dispatch (Ia_ToggleFeedback(chat.Id))
                    dispatch (Ia_Feedback_Submit(chat.Id)))
                "Submit"
            }
            comp<MudIconButton> {
                "Icon" => Icons.Material.Outlined.Cancel
                "Class" => "align-self-start"
                "Title" => "Close"
                on.click (fun _ ->
                    dispatch (Ia_ToggleFeedback(chat.Id)))
            }
    }

(*
*)
type MessageView() =
    inherit ElmishComponent<bool*Interaction*InteractionMessage*Model,Message>()

    let iconType (c:InteractionMessage)  = if c.IsUser then Icons.Material.Filled.Person else Icons.Material.Filled.Assistant

    let icon (c:InteractionMessage) =
        comp<MudIcon> {
            "Style" => "padding-right:10px;"
            "Icon" => iconType c
            "Size" => Size.Medium
        }

    let thumbsUpDn model (chat:Interaction) dispatch =
        concat {
            match chat.Feedback with
            | None -> ()
            | Some fb ->
                let icon =
                    if fb.ThumbsUpDn > 0 then Icons.Material.Outlined.ThumbUp
                    elif fb.ThumbsUpDn < 0 then Icons.Material.Outlined.ThumbDown
                    else Icons.Material.Outlined.ThumbsUpDown
                let color =
                    if fb.ThumbsUpDn > 0 then Color.Success
                    elif fb.ThumbsUpDn < 0 then Color.Error
                    else Color.Default
                if chat.IsBuffering |> not && chat.Feedback.IsSome then
                    comp<MudFab>{
                    //comp<MudIconButton>{
                        "Class" => "align-self-end mb-2"
                        "Color" => color
                        "Size" => Size.Small
                        "Disabled" => (TmpState.isFeedbackOpen chat.Id model)
                        "StartIcon" => icon
                        "Title" => "Feedback"
                        on.click (fun _ -> dispatch (Ia_ToggleFeedback(chat.Id)))
                    }
        }

    let padding (c:InteractionMessage) = if c.IsUser then "margin-right:20px;" else "margin-left:20px"

    let border (c:InteractionMessage) = if c.IsUser then "mud-border-primary" else "mud-border-warning"

    override this.View model dispatch =
        let isBusy,chat,msg,model = model
        let docs = match msg.Role with Assistant r -> r.Docs | _ -> []
        let isAsst = match msg.Role with Assistant _ -> true | _ -> false
        let backColor = if model.darkTheme then Colors.Gray.Darken4 else Colors.Amber.Lighten4
        let isLastAsst = chat.Messages |> List.tryLast |> Option.map(fun m -> m.MsgId = msg.MsgId && isAsst) |> Option.defaultValue false

        comp<MudPaper> {
            "Class" => $"d-flex border-solid border flex-column {border msg} rounded-lg pa-1"
            "Style" => $"{padding msg}; background-color:{backColor};"
            //"Style" => $"{padding msg} border-solid border-5 mud-border-primary"
            "Elevation" => 0
            comp<MudPaper> {
                "Class" => "d-flex flex-row "
                "Style" => $"background-color:{backColor};"
                "Elevation" => 0
                comp<MudPaper> {
                    "Class" => "d-flex flex-grow-1 ma-1 overflow-auto"
                    "Style" => $"background-color:{backColor};"
                    "Elevation" => 0
                    concat {
                        icon msg
                        // if not isLastAsst then
                        // else
                        //     comp<MudPaper> {
                        //         "Class" => "d-flex flex-column"
                        //         "Elevation" => 0
                        //         "Style" => $"background-color:{backColor};"
                        //         icon msg
                        //     }
                        div {
                            attr.style "white-space: pre-line;"
                            Model.blockQuotes msg.Message
                        }
                    }
                }
                comp<MudPaper> {
                    "Class" => "d-flex flex-none align-start ma-1"
                    "Elevation" => 0
                    "Style" => $"background-color:{backColor};"
                    if msg.IsUser then
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Outlined.RestartAlt
                            "Size" => Size.Small
                            "Title" => "Restart chat from here"
                            "Disabled" => chat.IsBuffering
                            on.click(fun e -> dispatch (Ia_Restart (chat.Id,msg)))
                        }
                    if isLastAsst then
                        comp<MudSpacer> { attr.empty() }
                        thumbsUpDn model chat dispatch
                }
            }
            if not chat.IsBuffering then
                table {
                    if not docs.IsEmpty && not chat.IsBuffering then
                        tr {
                            td {
                                attr.style "width: 1.5rem;"
                                comp<MudIconButton> {
                                    "Class" => "align-self-center"
                                    "Title" => "Show search results"
                                    "Icon" => Icons.Material.Outlined.SnippetFolder
                                    on.click (fun _ -> dispatch (Ia_ToggleDocs (chat.Id, Some msg.MsgId)))
                                }
                            }
                            td {
                                comp<MudPaper> {
                                    "Class" => "pa-2 overflow-auto"
                                    "Style" => "height: 3.5rem;"
                                    for d in docs do
                                        comp<MudTooltip> {
                                            "Text" => Utils.shorten 40 d.Text
                                            comp<MudLink> {
                                                "Class" => "ml-2 align-self-center"
                                                "Style" => "max-width: 140px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;"
                                                "Href" => d.Ref
                                                "Target" => "_blank"
                                                d.Title
                                            }
                                        }
                                }
                            }
                        }
                }
                if isLastAsst && chat.Feedback.IsSome then
                    ecomp<FeedbackViewB,_,_> (chat.Feedback.Value,chat,model) dispatch {attr.empty()}
        }
