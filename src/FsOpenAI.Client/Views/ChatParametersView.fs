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
                                "Label" => "Mode"
                                comp<MudButtonGroup> {
                                    "Variant" => Variant.Filled
                                    "Class" => "d-flex self-align-center justify-center"
                                    for m in Interaction.getModeCases() do
                                        let uc,vs = Interaction.getModeCase chat.Parameters.Mode
                                        let c = FSharpValue.MakeUnion (m,vs) :?> ExplorationMode
                                        let color =
                                            if uc.Name = m.Name then
                                                if model.darkTheme then
                                                    model.theme.PaletteDark.Primary.Value
                                                else
                                                    model.theme.PaletteLight.Primary.Value
                                            else ""
                                        comp<MudButton> {
                                            "Style" => $"color:{color}"
                                            on.click (fun e -> dispatch (Ia_UpdateParms (chat.Id, {chat.Parameters with Mode = c})))
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
                                comp<MudTooltip> {
                                    "Placement" => Placement.Right
                                    "Text" => "If enabled, search method will also use literal terms (e.g. keywords, etc.), in addition to semantic/conceptual content"
                                    comp<MudSwitch<bool>> {
                                        "Class" => "px-4 mr-4"
                                        "Color" => if bag.HybridSearch then Color.Tertiary else Color.Default
                                        "Label" => "Hybrid Search"
                                        "Value" => bag.HybridSearch
                                        attr.callback "ValueChanged" (fun (e:bool) -> dispatch (Ia_UpdateQaBag (chat.Id,{bag with HybridSearch = e})))
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
