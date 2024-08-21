namespace FsOpenAI.Client.Views
open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Elmish
open MudBlazor
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
        let height = if this.Model.QaBag.IsSome then "32rem" else "23rem"
    
        let searchTooltip = function
            | SearchMode.Semantic -> "Search with meaning, e.g. 'small' should match 'tiny', 'little', 'not big', etc."
            | SearchMode.Keyword -> "Search using exact keyword matches. Useful for product codes, acronyms, etc. USE only if other modes not effective."
            | SearchMode.Hybrid -> "A mix of Semantic and Keyword (default, generally best)"

        concat {
            comp<RadzenButton> {
                "Style" => "background:transparent;height:2rem;"
                "Icon" => "more_horiz"
                "ButtonStyle" => ButtonStyle.Base
                attr.callback "Click" (fun (e:MouseEventArgs) -> popup.Value |> Option.iter (fun p -> p.ToggleAsync(button.Value.Value.Element) |> ignore))
                button
            }
            comp<Popup> {
                "Style" => $"display:none;position:absolute;max-height:90vh;max-width:90vw;height:{height};width:25rem;padding:5px;border:var(--rz-panel-border);background-color:var(--rz-panel-background-color); overflow: auto;"
                "Lazy" => false
                attrext.callback "Close" this.Close 
                popup
                comp<RadzenStack> { 
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
(*
type ChatParametersView() =
    inherit ElmishComponent<bool*Interaction*Model,Message>()

    override this.View mdl (dispatch:Message -> unit) =
        let settingsOpen,chat,model = mdl
        let backends = model.appConfig.EnabledBackends

        let buttonSytle isSelected =
            if isSelected then
                let c =
                    if model.darkTheme then
                        model.theme.PaletteDark.Primary.Value
                    else
                        model.theme.PaletteLight.Primary.Value
                $"color:{c}; text-decoration: underline;"
            else
                ""

        let searchTooltip = function
            | SearchMode.Semantic -> "Search with meaning, e.g. 'small' should match 'tiny', 'little', 'not big', etc."
            | SearchMode.Keyword -> "Search using exact keyword matches. Useful for product codes, acronyms, etc. USE only if other modes not effective."
            | SearchMode.Hybrid -> "A mix of Semantic and Keyword (default, generally best)"

        concat {
            comp<MudPopover> {
                    "AnchorOrigin" => Origin.TopLeft
                    "TransformOrigin" => Origin.TopLeft
                    "Open" => settingsOpen
                    comp<MudPaper> {
                        "Outlined" => true
                        "Class" => "py-4 d-flex flex-column"
                        comp<MudPaper> {
                            "Class" => "d-flex flex-row"
                            comp<MudText> {
                                "Class" => "px-4"
                                "Typo" => Typo.h6
                                "Settings"
                            }
                            comp<MudSpacer>{
                                attr.empty()
                            }
                            comp<MudIconButton> {
                                "Class" => "align-self-end"
                                "Icon" => Icons.Material.Filled.Close
                                on.click (fun e -> dispatch (Ia_ToggleSettings chat.Id))
                            }
                        }
                        comp<MudPaper> {
                            "Elevation" => 0
                            comp<MudField> {
                                "Class" => "ma-2 d-flex"
                                "Variant" => Variant.Outlined
                                "Label" => "Generation Mode (temperature)"
                                comp<MudButtonGroup> {
                                    "Variant" => Variant.Filled
                                    "Elevation" => 0
                                    "Class" => "d-flex"
                                    for m in Interaction.getExplorationModeCases() do
                                        let uc,vs = Interaction.getExplorationModeCase chat.Parameters.Mode
                                        let c = FSharpValue.MakeUnion (m,vs) :?> ExplorationMode
                                        comp<MudButton> {
                                            "Class" => "d-flex flex-grow-1"
                                            "Style" => (buttonSytle (uc.Name = m.Name))
                                            on.click (fun _ -> dispatch (Ia_UpdateParms (chat.Id, {chat.Parameters with Mode = c})))
                                            text m.Name
                                        }
                                }
                            }
                            comp<MudSlider<int>> {
                                "Class" => "px-4"
                                "Min" => 600
                                "Max" => 4096
                                "Step" => 300
                                "ValueLabel" => true
                                "Value" => chat.Parameters.MaxTokens
                                on.change (fun e -> dispatch (Ia_UpdateParms (chat.Id,{chat.Parameters with MaxTokens = (e.Value :?> string |> int)})))
                                text $"Max Tokens: {chat.Parameters.MaxTokens}"
                            }
                            comp<MudSelect<Backend>> {
                                "Class" => "ma-2 d-flex"
                                "Variant" => Variant.Outlined
                                "Label" => "Backend"
                                "Value" => chat.Parameters.Backend
                                attr.callback "SelectedValuesChanged"  (fun (vs:Backend seq) ->
                                    dispatch (Ia_UpdateParms (chat.Id,{chat.Parameters with Backend = Seq.head vs})))
                                for b in backends do
                                    comp<MudSelectItem<Backend>> {
                                        "Selected" => (b = chat.Parameters.Backend)
                                        "Value" => b
                                        text (string b)
                                    }
                            }
                            match Interaction.qaBag chat with
                            | Some bag ->
                                comp<MudField> {
                                    "Class" => "ma-2 d-flex"
                                    "Variant" => Variant.Outlined
                                    "Label" => "Search Mode"
                                    comp<MudButtonGroup> {
                                        "Variant" => Variant.Filled
                                        "Class" => "d-flex"
                                        for m in Interaction.getSearchModeCases() do
                                            let uc,vs = Interaction.getSearchModeCase bag.SearchMode
                                            let c = FSharpValue.MakeUnion (m,vs) :?> SearchMode
                                            comp<MudButton> {
                                                "Style" => (buttonSytle (uc.Name = m.Name))
                                                "Class" => "d-flex flex-grow-1"
                                                on.click (fun _ -> dispatch (Ia_UpdateQaBag (chat.Id,{bag with SearchMode = c})))
                                                comp<MudTooltip> {
                                                    "Text" => (searchTooltip c)
                                                    "Placement" => Placement.Right
                                                    "Class" => "d-flex flex-grow-1"
                                                    text m.Name
                                                }
                                            }
                                    }
                                }
                                comp<MudSlider<int>> {
                                    "Class" => "px-4"
                                    "Min" => 1
                                    "Max" => 30
                                    "Step" => 1
                                    "ValueLabel" => true
                                    "Value" => bag.MaxDocs
                                    on.change (fun e -> dispatch (Ia_UpdateQaBag (chat.Id,{bag with MaxDocs = (e.Value :?> string |> int)})))
                                    text $"Max Documents: {bag.MaxDocs}"
                                }
                            | _ -> ()
                        }
                    }
                }
        }
*)
