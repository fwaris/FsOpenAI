namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared

type MainSettingsView() =
    inherit ElmishComponent<Model,Message>()    
    override this.View mdl (dispatch:Message -> unit) =
        let settingsOpen = Utils.isOpen C.MAIN_SETTINGS mdl.settingsOpen
        comp<MudPopover> {
                "Style" => "margin-top:3rem; margin-right:5rem; width:75%; max-width:25rem;"
                "AnchorOrigin" => Origin.TopRight
                "TransformOrigin" => Origin.TopRight
                "Open" => settingsOpen
                "Paper" => true
                "Outlined" => true
                comp<MudStack> {
                    comp<MudStack> {
                        "Row" => true
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
                            on.click (fun e -> dispatch (OpenCloseSettings C.MAIN_SETTINGS))
                        }
                    }
                    comp<MudTextField<string>> {
                        "Label" => "OpenAI Key"
                        "Class" => "ma-2"
                        "InputType" => InputType.Password
                        attr.callback "ValueChanged" (fun e -> dispatch (UpdateOpenKey e))
                        "Variant" => Variant.Outlined
                        "Placeholder" => "key stored locally"
                        "Text" => (mdl.serviceParameters |> Option.bind(fun p -> p.OPENAI_KEY) |> Option.defaultValue "")
                    }
                }
            }
        
