namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open Microsoft.AspNetCore.Components.Web
open Microsoft.JSInterop
open Microsoft.AspNetCore.Components
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions

type QuestionView() =
    inherit ElmishComponent<Model,Message>()

    let questionRef = Ref<MudTextField<string>>()

    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set

    override this.View model dispatch =
        let selChat = Model.selectedChat model
        let dbag = selChat |> Option.map (fun c -> c.InteractionType) |> Option.bind (function IndexQnADoc d -> Some d | _-> None)
        concat {
            if model.interactions.Length > 0 then 
                comp<MudPaper> {
                    "Class" => "d-flex border-solid border flex-row mud-border-primary rounded-lg pa-1 ma-4" 
                    "Elevation" => 5
                    concat {
                        comp<MudPaper> {
                            "Class" => "ma-4 d-flex align-self-center flex-column"
                            "Elevation" => 0    
                            comp<MudIconButton> {
                                "Icon" => Icons.Material.Outlined.DeleteSweep
                                "Title" => "Clear chat for new topic"
                                "Disabled" => (selChat |> Option.map(fun x->x.IsBuffering) |> Option.defaultValue true)
                                "Size" => if dbag.IsSome then Size.Small else Size.Medium
                                on.click(fun e -> 
                                    selChat 
                                    |> Option.iter(fun ch -> dispatch (Ia_ResetChat (ch.Id,""))))
                            }
                            match dbag with
                            | Some dbag -> 
                                comp<MudToggleIconButton> {
                                    "Icon" => Icons.Material.Outlined.FindInPage
                                    "Title" => "Document-only query mode: Off"
                                    "Disabled" => (selChat |> Option.map(fun x->x.IsBuffering) |> Option.defaultValue true)
                                    "ToggledIcon" => Icons.Material.Filled.FindInPage
                                    "ToggledColor" => Color.Warning
                                    "ToggledTitle" => "Document-only query mode: On"
                                    "Toggled" => dbag.DocOnlyQuery
                                    "Size" => Size.Small
                                    "ToggledSize" => Size.Small
                                    on.click(fun e -> 
                                        selChat 
                                        |> Option.iter(fun ch -> dispatch (Ia_ToggleDocOnly (ch.Id))))
                                }
                            | _ -> ()
                        }
                        comp<MudTextField<string>> {
                            "Label" => "Question"
                            "Variant" => Variant.Outlined
                            "Lines" => 3
                            "Placeholder" => "Enter prompt or question"
                            "Text" => (selChat |> Option.map (fun c ->  c.Question) |> Option.defaultValue "")
                            attr.callback "OnBlur" (fun (e:FocusEventArgs) -> 
                                questionRef.Value
                                |> Option.iter(fun m -> 
                                    selChat
                                    |> Option.iter(fun ch ->
                                        dispatch (Ia_SetQuestion (ch.Id,m.Text)))))
                            on.keydown(fun e -> 
                                if e.Key = "Enter" && not e.ShiftKey then                                     
                                    questionRef.Value
                                    |> Option.iter(fun (m:MudTextField<string>) -> 
                                        m.BlurAsync() |> ignore
                                        selChat 
                                        |> Option.iter(fun ch -> dispatch (Ia_SubmitOnKey (ch.Id,true))))
                                        )
                            questionRef
                        }
                        comp<MudIconButton> {
                            "Class" => "ma-4 d-flex align-self-center"
                            "Icon" => Icons.Material.Filled.Send
                            "Disabled" => (selChat |> Option.map(Submission.isReady>>not) |> Option.defaultValue true)
                            "Size" => Size.Medium
                            on.click(fun e -> 
                                questionRef.Value
                                |> Option.iter(fun m -> 
                                    selChat 
                                    |> Option.iter(fun ch -> dispatch (Ia_Submit (ch.Id,m.Text)))))

                        }                  
                    }
                }
        }
