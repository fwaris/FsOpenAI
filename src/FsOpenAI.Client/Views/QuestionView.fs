namespace FsOpenAI.Client.Views
open System
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Microsoft.JSInterop
open Bolero
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open MudBlazor
open Radzen
open Radzen.Blazor

type QuestionView() = 
    inherit ElmishComponent<Model, Message>()
    let input = Ref<RadzenTextArea>()

    [<Inject>] member val JSRuntime : IJSRuntime = Unchecked.defaultof<_> with get, set

    [<Inject>] member val TooltipService : TooltipService = Unchecked.defaultof<_> with get, set

    override this.View model dispatch = 
        let selChat = Model.selectedChat model
        let isNotReady = not (Submission.isReady selChat)
        let question = selChat |> Option.map (fun c -> c.Question) |> Option.defaultValue null
        comp<RadzenCard> {
            attr.``class`` "rz-shadow-5 rz-border-radius-5 rz-p-2"            
            "Variant" => Variant.Outlined
            "Style" => "width: 100%;"
            comp<RadzenStack> { 
                "Orientation" => Orientation.Horizontal
                "Gap" => "0.1rem"
                comp<RadzenMenu> {
                    "Style" => "background-color: transparent;"
                    "Responsive" => false                        
                    comp<RadzenMenuItem> {
                        "Icon" => "delete_sweep"
                        attr.title "Clear chat for new topic"
                        attr.callback "Click" (fun (e:MenuItemEventArgs) -> 
                            selChat 
                            |> Option.iter (fun c -> dispatch (Ia_ResetChat (c.Id,""))))
                    }
                }
                comp<RadzenTextArea> {                            
                    "Rows" => 3 
                    attr.id "idQuestion"
                    on.keydown (fun e -> 
                        if not e.ShiftKey && e.Key = "Enter" && Submission.isReady selChat then  
                            task{
                                let! text = this.JSRuntime.InvokeAsync<string>("eval", """document.getElementById("idQuestion").value""")
                                dispatch (Ia_Submit (selChat.Value.Id,text))
                            } |> ignore)
                    "Placeholder" => match selChat with Some _ -> "Type your question here" | _ -> "Select or add a chat to start"
                    attr.disabled selChat.IsNone
                    "Style" => "resize: none; width: 100%; outline: none; border: none;border-bottom: 2px solid var(--rz-primary);"
                    "Value" => question
                    input                        
                }                    
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    "AlignItems" => AlignItems.End
                    comp<RadzenSpeechToTextButton> {
                        "Title" => "Start recording"
                        attr.disabled selChat.IsNone
                        attr.``class`` "rz-ml-1"
                        attr.callback "Change" (fun (e:string) ->
                            selChat
                            |> Option.iter (fun c -> dispatch (Ia_SetQuestion (c.Id,e))))        
                    }
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Vertical
                        comp<RadzenMenu> {
                            "Responsive" => false
                            "Style" => "background-color: transparent;"
                            comp<RadzenMenuItem> {
                                "Icon" => "send"
                                attr.disabled  isNotReady
                                attr.title "Send"
                                attr.callback "Click" (fun (e:MenuItemEventArgs) -> 
                                    input.Value
                                    |> Option.map(fun i -> i.Value)
                                    |> Option.bind (fun v -> selChat |> Option.map (fun c -> c.Id,v))
                                    |> Option.iter (fun (id,v) -> dispatch (Ia_Submit (id,v)))
                                    )
                            }
                        }
                        comp<RadzenMenu> {
                            "Responsive" => false
                            "Style" => "background-color: transparent;"
                            comp<RadzenMenuItem> {
                                attr.disabled  isNotReady
                                "Icon" => "attach_file"
                                attr.title "Attach file"
                                attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch ToggleSideBar)
                            }
                        }
                    }
                }
            }
        }
(*
        comp<RadzenCard> {
            attr.``class`` "rz-shadow-5 rz-border-radius-5 rz-p-2"            
            "Variant" => Variant.Outlined
            "Style" => "width: 100%;"
            comp<RadzenRow> { 
                "Gap" => "0"
                comp<RadzenColumn> {
                    "Size" => 1
                    comp<RadzenMenu> {
                        "Style" => "background-color: transparent;"
                        "Responsive" => false                        
                        comp<RadzenMenuItem> {
                            "Icon" => "delete_sweep"
                            attr.title "Clear chat for new topic"
                            attr.callback "Click" (fun (e:MenuItemEventArgs) -> 
                                selChat 
                                |> Option.iter (fun c -> dispatch (Ia_ResetChat (c.Id,""))))
                        }
                    }
                }
                comp<RadzenColumn> {                        
                    "Size" => 9
                    "SizeSM"=> 8
                    comp<RadzenTextArea> {                            
                        "Rows" => 3 
                        "Placeholder" => "Type your question here"
                        "Style" => "resize: none; width: 100%; outline: none; border: none;border-bottom: 2px solid var(--rz-secondary);"
                        "Value" => question
                        input                        
                    }                    
                }
                comp<RadzenColumn> {
                    "Size" => 1
                    comp<RadzenSpeechToTextButton> {
                        "Title" => "Start recording"                     
                        attr.callback "Change" (fun (e:string) ->
                            selChat
                            |> Option.iter (fun c -> dispatch (Ia_SetQuestion (c.Id,e))))        
                    }
                }
                comp<RadzenColumn> {
                    "Size" => 1

                    comp<RadzenMenu> {
                        "Responsive" => false
                        "Style" => "background-color: transparent;"
                        comp<RadzenMenuItem> {
                            "Icon" => "send"
                            attr.title "Send"
                            attr.callback "Click" (fun (e:MenuItemEventArgs) -> 
                                input.Value
                                |> Option.map(fun i -> i.Value)
                                |> Option.bind (fun v -> selChat |> Option.map (fun c -> c.Id,v))
                                |> Option.iter (fun (id,v) -> dispatch (Ia_Submit (id,v)))
                                )
                        }
                    }
                    comp<RadzenMenu> {
                        "Responsive" => false
                        "Style" => "background-color: transparent;"
                        comp<RadzenMenuItem> {
                            "Icon" => "attach_file"
                            attr.title "Attach file"
                            attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch ToggleSideBar)
                        }
                    }
                }
            }
        }
*)

(*

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

*)