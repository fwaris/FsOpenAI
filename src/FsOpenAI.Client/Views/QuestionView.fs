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
open Radzen
open Radzen.Blazor
open Microsoft.AspNetCore.Components.Forms

type QuestionView() =
    inherit ElmishComponent<Model, Message>()
    let qInput = Ref<RadzenTextArea>()

    [<Inject>] member val JSRuntime : IJSRuntime = Unchecked.defaultof<_> with get, set

    member this.GetText() =
        task{
            let! text = this.JSRuntime.InvokeAsync<string>("eval", """document.getElementById("idQuestion").value""")
            return text
        }

    override this.View model dispatch =
        let selChat = Model.selectedChat model
        let isNotReady = not (Submission.isReady selChat)
        let isReasoning = selChat |> Option.map (fun c -> match c.Parameters.ModelType with MT_Logic -> true | _ -> false) |> Option.defaultValue false
        let question = selChat |> Option.map (fun c -> c.Question) |> Option.defaultValue null
        comp<RadzenCard> {
            attr.``class`` "rz-shadow-5 rz-border-radius-5 rz-p-2"
            "Variant" => Variant.Outlined
            "Style" => "width: 100%;"
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "Gap" => "0.1rem"
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    "Gap" => "0.1rem"
                    comp<RadzenMenu> {
                        "Style" => "background-color: transparent;"
                        "Responsive" => false
                        comp<RadzenMenuItem> {
                            "Icon" => "delete_sweep"
                            attr.disabled  isNotReady
                            attr.title "Clear chat for new topic"
                            attr.callback "Click" (fun (e:MenuItemEventArgs) ->
                                selChat
                                |> Option.iter (fun c -> dispatch (Ia_ResetChat (c.Id,""))))
                        }
                    }
                    comp<RadzenMenu> {
                        "Style" => "background-color: transparent;"
                        "Responsive" => false
                        comp<RadzenMenuItem> {
                            "Icon" => "psychology"
                            "IconColor" => (if isReasoning then Colors.Primary else Colors.Info)
                            attr.disabled  isNotReady
                            attr.title (if isReasoning then "Reasoning mode on" else "Reasoning mode off")
                            attr.callback "Click" (fun (e:MenuItemEventArgs) ->
                                selChat
                                |> Option.iter (fun c -> dispatch (Ia_ToggleModelType (c.Id))))
                        }
                    }
                }
                comp<RadzenTextArea> {
                    "Rows" => 3
                    attr.id "idQuestion"
                    on.keydown (fun e ->
                        if not e.ShiftKey && e.Key = "Enter" && Submission.isReady selChat then
                            task {
                                let! text = this.GetText()
                                dispatch (Ia_SetQuestion (selChat.Value.Id,text))
                                dispatch (Ia_SubmitOnKey (selChat.Value.Id,true))
                            } |> ignore)
                    on.blur (fun e ->
                        task{
                            let! text = this.GetText()
                            dispatch (Ia_SetQuestion (selChat.Value.Id,text))
                        } |> ignore)
                    "Placeholder" => match selChat with Some _ -> "Type your question here" | _ -> "Select or add a chat to start"
                    attr.disabled selChat.IsNone
                    "Style" => "resize: none; width: 100%; outline: none; border: none;border-bottom: 2px solid var(--rz-primary);"
                    "Value" => question
                    qInput
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
                                    qInput.Value
                                    |> Option.map(fun i -> i.Value)
                                    |> Option.bind (fun v -> selChat |> Option.map (fun c -> c.Id,v))
                                    |> Option.iter (fun (id,v) -> dispatch (Ia_Submit (id,v)))
                                    )
                            }
                        }
                        comp<InputFile> {
                            "Style" => "position: absolute; height: 0px; width: 0px; outline: none; padding: 0; margin: -1px; overflow: hidden; border:0; clip: rect(0,0,0,0)"
                            attr.id "inputGroupFileAddon01"
                            attr.disabled  isNotReady
                            attr.callback "OnChange" (fun (e:InputFileChangeEventArgs) ->
                                Model.selectedChat model
                                |> Option.iter (fun ch ->
                                    let content = {DocumentContent.Default with DocumentRef = Some e.File; DocType = IO.docType e.File.Name}
                                    dispatch (Ia_File_BeingLoad2 (ch.Id,content)))
                            )
                        }
                        comp<RadzenLabel> {
                            "Component" => "inputGroupFileAddon01"
                            "Style" => "cursor: pointer;"
                            comp<RadzenIcon> {
                                "Icon" => "attach_file"
                                "Style" => "background-color: transparent;"
                                attr.disabled  isNotReady
                                attr.title "Load a document or image"
                             }
                        }
                    }
                }
            }
        }

