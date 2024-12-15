namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open Radzen
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Radzen.Blazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open System.Collections.Generic
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions.CodeEval

[<CLIMutable>]
type CodeGenPromptModel = 
    {
        ChatId                  : string
        Model                   : Model
        CodeEvalParms           : CodeEvalParms
        mutable CodePrompt      : string
        mutable RegenTemplate   : string
    }

type CodePromptsDialog() =
    inherit ElmishComponent<CodeGenPromptModel,Message>()

    [<Inject>]
    member val DialogService  = Unchecked.defaultof<DialogService> with get, set

    member this.Close() =
        let codePrompt = if Utils.notEmpty this.Model.CodePrompt then Some this.Model.CodePrompt else None
        let regenTemplate = if Utils.notEmpty this.Model.RegenTemplate then Some this.Model.RegenTemplate else None
        let p = {this.Model.CodeEvalParms with CodeGenPrompt = codePrompt; RegenPrompt = regenTemplate }
        this.Dispatch (Ia_UpdateCodeEvalParms (this.Model.ChatId, p))
        this.DialogService.Close()

    override this.View model dispatch =
        concat {
            style {                                        
                ".rz-dialog-content { height: 100%; }"
                ".rz-tabview-panel {height: 100%; }"            
            } 
            comp<RadzenStack> {
                "Style" => "height: 100%;"
                comp<RadzenTabs> {
                    "Style" => "height:100%"
                    attr.fragment "Tabs" (                    
                        concat {                    
                            comp<RadzenTabsItem> {
                                "Text" => "Code Gen System Message"
                                comp<RadzenTextArea> {
                                    attr.style "white-space: pre-wrap; font-family: monospace; resize:none; width:100%; height:100%;"                        
                                    "Rows" => 3
                                    "Cols" => 30
                                    Bind.InputExpression.string 
                                        <@ Func<_>(fun () -> this.Model.CodePrompt) @> 
                                        (fun v -> this.Model.CodePrompt <- v)
                                }                            
                            }                
                            comp<RadzenTabsItem> {
                                "Text" => "Fix Compile Errors Template"
                                comp<RadzenTextArea> {
                                    attr.style "white-space: pre-wrap; font-family: monospace; resize:none; width:100%; height:100%;"                        
                                    "Rows" => 3
                                    "Cols" => 30
                                    Bind.InputExpression.string 
                                        <@ Func<_>(fun () -> this.Model.RegenTemplate) @> 
                                        (fun v -> this.Model.RegenTemplate <- v)
                                }                            
                            }
                        }
                    )
                }
                comp<RadzenButton> {
                    "Style" => "max-width: 5rem"
                    attr.callback "Click" (fun (e:MouseEventArgs) -> this.Close())
                    "Save"
                }
            }
        }

type CodeEvalView() =
    inherit ElmishComponent<Model,Message>()

    [<Inject>]
    member val DialogService  = Unchecked.defaultof<DialogService> with get, set

    override this.View model dispatch =
        let mode = Model.selectedChat model |> Option.map (fun x -> x.Mode) |> Option.defaultValue M_Plain
        comp<RadzenStack> {
            "Orientation" => Orientation.Horizontal
            "AlignItems" => AlignItems.Center
            comp<RadzenCheckBox<bool>> {
                attr.title "Experimental! Generate and evalualte F# code "
                "Value" => (mode = M_CodeEval)
                attr.callback "Change" (fun (v:bool) ->
                    Model.selectedChat model |> Option.iter (fun chat ->
                        dispatch (Ia_Mode_CodeEval chat.Id)))
            }
            comp<RadzenLabel> {
                "Text" => "Code Evaluator"
            }
            comp<RadzenButton> {
                "Style" => "background:transparent;height:2rem;"
                "Icon" => "more_horiz"
                attr.title "Document details"
                "ButtonStyle" => ButtonStyle.Base
                attr.callback "Click" (fun (e:MouseEventArgs) ->
                    match Model.selectedChat model with 
                    | Some ch -> 
                        let codeBag = Interactions.CodeEval.Interaction.codeBag ch 
                        let evalPrompts = codeBag |> Option.map (_.CodeEvalParms) |> Option.defaultValue CodeEvalParms.Default
                        let codePrompt = evalPrompts.CodeGenPrompt |> Option.defaultValue Interactions.CodeEval.CodeEvalPrompts.sampleCodeGenPrompt
                        let regenTemplate = evalPrompts.RegenPrompt |> Option.defaultValue Interactions.CodeEval.CodeEvalPrompts.regenPromptTemplate
                        let model = {ChatId = ch.Id; Model=model; CodeEvalParms=evalPrompts; CodePrompt=codePrompt; RegenTemplate=regenTemplate}                            
                        let parms = ["Model",model :> obj; "Dispatch",dispatch] |> dict |> Dictionary
                        let opts = DialogOptions(Width = "70%", Height="70%")
                        this.DialogService.OpenAsync<CodePromptsDialog>("Code Gen Prompts", parameters=parms, options=opts) |> ignore
                    | None -> ()
                )
            }              
        }

type CodeAndPlanViewDialog() =
    inherit ElmishComponent<Model,Message>()

    override this.View model dispatch =
        let codeBag = Model.selectedChat model |> Option.bind (fun ch -> Interactions.CodeEval.Interaction.codeBag ch)
        let genCode = codeBag |> Option.bind (_.Code)
        let genPlan = codeBag |> Option.bind (_.Plan)
        concat {
            style {                                        
                ".rz-dialog-content { height: 100%; }"
                ".rz-tabview-panel {height: 100%; }"
            }  
            comp<RadzenTabs> {
                "Style" => "height: 100%;"
                attr.fragment "Tabs" (
                concat {                  
                    comp<RadzenTabsItem> {                        
                        "Text" => "Generated Code"                        
                        div {
                            attr.style "white-space: pre-wrap; font-family: monospace; resize:none; width:100%; height:100%;"                        
                            pre{
                                code {
                                    text (genCode |> Option.defaultValue "")
                                }
                            }
                        }
                    }                
                    comp<RadzenTabsItem> {
                        attr.title "Plan created prior to code gen (optional)"                        
                        "Text" => "Plan"
                        comp<RadzenTextBox> {
                            "Style" => "white-space: pre-line; width:100%; height:100%;"
                            "Cols" => 10
                            "Rows" => 10
                            "ReadOnly" => true
                            "Value" => text (genPlan |> Option.defaultValue "" )
                        }
                    }
                }
            )
        }
    }

type CodeAndPlanView() =
    inherit ElmishComponent<Model,Message>()

    [<Inject>]
    member val DialogService  = Unchecked.defaultof<DialogService> with get, set

    override this.View model dispatch =
        comp<RadzenStack> {
            attr.``class`` "rz-p-1 "
            "AlignItems" => AlignItems.End
            "Orientation" => Orientation.Horizontal
            "Shade" => Shade.Lighter
            "JustifyContent" => JustifyContent.End
            comp<RadzenButton> {
                attr.``class`` "rz-border-radius-6 rz-shadow-3"
                "ButtonStyle" => ButtonStyle.Base
                "Variant" => Variant.Outlined
                "ButtonSize" => ButtonSize.ExtraSmall
                attr.callback "Click" (fun (e:MouseEventArgs) ->
                    let parms = ["Model",model :> obj; "Dispatch",dispatch] |> dict |> Dictionary
                    let opts = DialogOptions(Width = "70%", Height="70%")
                    this.DialogService.OpenAsync<CodeAndPlanViewDialog>("Generated Code and Plan", parameters=parms, options=opts) |> ignore
                )   
                "code"
            } 
        }
