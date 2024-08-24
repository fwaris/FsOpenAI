namespace FsOpenAI.Client.Views
open System
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open FSharp.Reflection
open Radzen.Blazor.Rendering

module attrext =
    open Microsoft.AspNetCore.Components
    let inline callback (name: string) ([<InlineIfLambda>] value: unit -> unit) =
        Attr(fun receiver builder sequence ->
            builder.AddAttribute(sequence, name, EventCallback.Factory.Create(receiver, Action(value)))
            sequence + 1)

//need local binding for Radzen Popup as dispatch (page rendering) closes the popup
[<CLIMutable>]
type PModel = {
    ChatId : string
    Model : Model
    mutable Parms : InteractionParameters
    mutable QaBag : QABag option
    mutable SystemMessage: string
}

type ChatSettingsView() =
    inherit ElmishComponent<PModel,Message>()
    let button = Ref<RadzenButton>()
    let popup = Ref<Popup>()

    let mutable initMaxDocs = 0
    let mutable initMaxTokens = 0

    //Note: dispatch re-renders the page and changes are lost - 
    //*dispatch should be the very last step(s) at popup close*
    member this.Close() =         
        [
            Ia_UpdateParms (this.Model.ChatId, this.Model.Parms)
            Ia_SystemMessage (this.Model.ChatId, this.Model.SystemMessage)                
            match this.Model.QaBag with
            | Some bag -> (Ia_UpdateQaBag (this.Model.ChatId, bag))
            | None -> ()
        ]
        |> List.iter this.Dispatch

    override this.OnParametersSet() = 
        initMaxDocs <- this.Model.QaBag |> Option.map (fun b -> b.MaxDocs) |> Option.defaultValue 0
        initMaxTokens <- this.Model.Parms.MaxTokens
            
    //need to re-render if changes made to these so locally linked fields are updated
    override this.ShouldRender() = 
        this.Model.QaBag |> Option.map (fun b -> b.MaxDocs) |> Option.defaultValue 0 <> initMaxDocs ||
        this.Model.Parms.MaxTokens <> initMaxTokens
        || base.ShouldRender()

    override this.View mdl (dispatch:Message -> unit) =
        let backends = this.Model.Model.appConfig.EnabledBackends
        let height = if this.Model.QaBag.IsSome then "36rem" else "28rem"
        let width = "30rem"
    
        let searchTooltip = function
            | SearchMode.Semantic -> "Search with meaning, e.g. 'small' should match 'tiny', 'little', 'not big', etc."
            | SearchMode.Keyword -> "Search using exact keyword matches. Useful for product codes, acronyms, etc. USE only if other modes not effective."
            | SearchMode.Hybrid -> "A mix of Semantic and Keyword (default, generally best)"

        concat {
            comp<RadzenButton> {
                "Style" => "background:transparent;height:2rem;"
                "Icon" => "more_horiz"
                attr.title "Settings"
                "ButtonStyle" => ButtonStyle.Base
                attr.callback "Click" (fun (e:MouseEventArgs) -> popup.Value |> Option.iter (fun p -> p.ToggleAsync(button.Value.Value.Element) |> ignore))
                button
            }
            comp<Popup> {
                "Style" => $"display:none;position:absolute;max-height:90vh;max-width:90vw;height:{height};width:{width};padding:5px;background:transparent;"
                "Lazy" => false
                attrext.callback "Close" this.Close 
                popup
                comp<RadzenCard> {
                    "Style" => "height:100%;width:100%;overflow:none;background-color:var(--rz-panel-background-color);"
                    //"Style" => "height:100%;width:100%;overflow:none;"
                    "Variant" => Variant.Filled
                    attr.``class`` "rz-shadow-5 rz-p-2 rz-border-radius-5 rz-border-danger-light"
                    comp<RadzenStack> { 
                        attr.``class`` "rz-p-5"
                        "Style" => "height:100%;width:100%;overflow:auto;background-color: var(--rz-panel-background-color);"
                        "AlignItems" => AlignItems.Center
                        comp<RadzenFieldset> {
                            "AllowCollapse" => false
                            "Text" => "Exploration Mode"
                            comp<RadzenRadioButtonList<ExplorationMode>> {
                                "Value" => this.Model.Parms.Mode
                                attr.callback "ValueChanged" (fun v -> this.Model.Parms <- {this.Model.Parms with Mode = v})
                                attr.fragment "Items" (
                                    concat {
                                        for m in Interaction.getExplorationModeCases() do
                                            let uc,vs = Interaction.getExplorationModeCase this.Model.Parms.Mode
                                            let c = FSharpValue.MakeUnion (m,vs) :?> ExplorationMode
                                            comp<RadzenRadioButtonListItem<ExplorationMode>> {
                                                "Value" => c
                                                "Text" => m.Name                            
                                            }
                                    })
                            }
                        }
                        comp<RadzenStack> {
                            "Orientation" => Orientation.Horizontal
                            "AlignItems" => AlignItems.Center
                            comp<RadzenLabel> {"Text" => "Backend"}
                            comp<RadzenDropDown<Backend>> {
                                "Data" => backends
                                "Value" => this.Model.Parms.Backend
                                attr.callback "ValueChanged"  (fun b -> this.Model.Parms <- {this.Model.Parms with Backend = b})
                            }
                        }
                        comp<RadzenStack> {
                            "Orientation" => Orientation.Horizontal
                            "AlignItems" => AlignItems.Center
                            comp<RadzenLabel> {"Text" => "Max Tokens"}
                            comp<RadzenSlider<int>> {
                                "Min" => 600m
                                "Max" => 4096m
                                "Step" => "300"
                                "Value" => this.Model.Parms.MaxTokens
                                Bind.InputExpression.intRaw 
                                    <@ Func<_>(fun () -> this.Model.Parms.MaxTokens) @> 
                                    (fun v -> this.Model.Parms <- {this.Model.Parms with MaxTokens = v })
                            }
                            comp<RadzenNumeric<int>> {     
                                "ReadOnly" => true
                                "ShowUpDown" => false
                                "Style" => "width: 4rem; background-color:var(--rz-panel-background-color);"
                                Bind.InputExpression.intRaw <@ Func<_>(fun () -> this.Model.Parms.MaxTokens) @> (fun v -> ())                                                                   
                            }
                            // }
                        }
                        match this.Model.QaBag with
                        | None -> ()
                        | Some _ -> 
                            comp<RadzenFieldset> {
                                "Text" => "Search Mode"
                                comp<RadzenRadioButtonList<SearchMode>> {
                                    "Value" => this.Model.QaBag.Value.SearchMode
                                    attr.callback "ValueChanged" (fun v -> this.Model.QaBag <- Some {this.Model.QaBag.Value with SearchMode = v})   
                                    attr.fragment "Items" (
                                        concat {
                                            for m in Interaction.getSearchModeCases() do
                                                let uc,vs = Interaction.getSearchModeCase this.Model.QaBag.Value.SearchMode
                                                let c = FSharpValue.MakeUnion (m,vs) :?> SearchMode
                                                comp<RadzenRadioButtonListItem<SearchMode>> {
                                                        attr.title (searchTooltip c)
                                                        "Value" => c
                                                        "Text" => m.Name                            
                                                    }
                                        })
                                }
                            }
                            comp<RadzenStack> {
                                "Orientation" => Orientation.Horizontal
                                "AlignItems" => AlignItems.Center
                                comp<RadzenLabel> {"Text" => "Max Documents"}
                                comp<RadzenSlider<int>> {
                                    "Min" => 1m
                                    "Max" => 30m
                                    "Step" => "1"
                                    Bind.InputExpression.intRaw 
                                        <@ Func<_>(fun () -> this.Model.QaBag.Value.MaxDocs) @> 
                                        (fun v -> this.Model.QaBag <- Some {this.Model.QaBag.Value with MaxDocs = v })
                                }
                                comp<RadzenNumeric<int>> {     
                                    "ReadOnly" => true
                                    "ShowUpDown" => false
                                    "Style" => "width: 3rem; background-color:var(--rz-panel-background-color);"
                                    Bind.InputExpression.intRaw <@ Func<_>(fun () -> this.Model.QaBag.Value.MaxDocs) @> (fun v -> ())                                                                   
                                }
                        }
                        comp<RadzenFieldset> {
                            "Text" => "System Message"
                            comp<RadzenTextArea> {
                                "Rows" => 3
                                "Cols" => 50
                                "MaxLength" => 3000L
                                Bind.InputExpression.string 
                                    <@ Func<_>(fun () -> this.Model.SystemMessage) @> 
                                    (fun v -> this.Model.SystemMessage <- v)
                            }
                        }                    
                    }
                }        
            }
        }
