namespace FsOpenAI.Client.Views
open System
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open FsOpenAI.Shared

type FeedbackView() =
    inherit ElmishComponent<Feedback*Interaction*Model,Message>()
    let commentRef = Ref<RadzenTextArea>()

    override this.View m dispatch =
        let fb,chat,model = m
        comp<RadzenStack> {
            attr.``class`` "rz-p-1"
            "AlignItems" => Radzen.AlignItems.Center
            "Orientation" => Radzen.Orientation.Horizontal
            let colorUp = if fb.ThumbsUpDn > 0 then "var(--rz-success)" else ""
            let colorDn = if fb.ThumbsUpDn < 0 then "var(--rz-danger)" else ""
            comp<RadzenTextArea> {
                "Placeholder" => "Comment (optional)"
                "Rows" => 1
                "Cols" => 30
                "Style" => "resize: none; width: 100%; outline: none;"
                "Value" => (fb.Comment |> Option.defaultValue "")
                on.blur (fun _ -> 
                    commentRef.Value
                    |> Option.iter(fun m ->
                        dispatch(Ia_Feedback_Set (chat.Id, {fb with Comment = Some m.Value}))))
                commentRef
            }
            comp<RadzenButton> {
                "Icon" => "thumb_up"
                "ButtonStyle" => ButtonStyle.Base
                "Style" => "background: transparent;"
                "Size" => ButtonSize.ExtraSmall
                "IconColor" => colorUp
                attr.callback "Click" (fun (e:MouseEventArgs) -> 
                    dispatch (Ia_Feedback_Set(chat.Id, {fb with ThumbsUpDn =  +1 }))
                    dispatch (Ia_Feedback_Submit(chat.Id)))
            }
            comp<RadzenButton> {
                "Icon" => "thumb_down"
                "IconColor" => colorDn
                "Style" => "background: transparent;"
                "ButtonStyle" => ButtonStyle.Base
                "Size" => ButtonSize.ExtraSmall
                attr.callback "Click" (fun (e:MouseEventArgs) -> 
                    dispatch (Ia_Feedback_Set(chat.Id, {fb with ThumbsUpDn =  -1}))
                    dispatch (Ia_Feedback_Submit(chat.Id)))
            }
        }
