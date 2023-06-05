module Prompt
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client.Model

type PromptView() =
    inherit ElmishComponent<Model,Message>()
    
    override this.View model dispatch =
        comp<MudGrid> {
            //"Class" => "d-flex flex-grow-1"
            "Spacing" => 0
            comp<MudItem> {
                //"Class" => "d-flex flex-grow-1"
                "xs" => 12
                comp<MudTextField<string>> {
                    attr.callback "ValueChanged" (fun e -> dispatch (SetPrompt e))
                    "Variant" => Variant.Outlined
                    "Label" => "Prompt"
                    "Lines" => 15
                    "Placeholder" => "Enter prompt or question"
                    "Text" => model.prompt
                }
            }
            comp<MudItem> {               
                //"Class" => "d-flex flex-grow-0"
                "xs" => 12
                comp<MudStack> {
                    "Row" => true
                    comp<MudIconButton> {
                        "Icon" => Icons.Material.Filled.DeleteSweep
                        "Tooltip" => "Clear data"
                        on.click(fun ev -> dispatch Reset )
                    }
                    comp<MudSpacer> {attr.empty()}
                    comp<MudIconButton> {
                        "Icon" => Icons.Material.Filled.Add
                        on.click(fun ev -> dispatch AddDummyContent)
                    }
                    comp<MudSpacer> {attr.empty()}
                    comp<MudIconButton> {    
                        "Icon" => Icons.Material.Filled.Send
                        on.click(fun ev -> dispatch SubmitChat )
                    }
                }
            }
        }

