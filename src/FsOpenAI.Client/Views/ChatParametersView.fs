namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open FSharp.Reflection

#nowarn "44"

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

type ChatSettingsView() =
    inherit ElmishComponent<Interaction*Model,Message>()

    override this.View mdl (dispatch:Message -> unit) =
        let chat,model = mdl
        let settingsOpen = TmpState.chatSettingsOpen chat.Id model
        concat {
            comp<MudIconButton> {
                "Class" => "d-flex flex-none align-self-center ma-2"
                "Icon" => Icons.Material.Outlined.Settings
                on.click(fun e -> dispatch (Ia_ToggleSettings chat.Id))
            }
            ecomp<ChatParametersView,_,_> (settingsOpen,chat,model) dispatch {attr.empty()}
        }
