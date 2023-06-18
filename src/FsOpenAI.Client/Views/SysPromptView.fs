module SysPrompt
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client.Model

type SysPromptView() =
    inherit ElmishComponent<Model,Message>()
    
    override this.View model dispatch =
        comp<MudTextField<string>> {              
            attr.callback "ValueChanged" (fun e -> dispatch (SetSystemPrompt e))
            "Variant" => Variant.Outlined
            "Label" => "System Prompt"
            "Lines" => 10
            "Placeholder" => "Set the 'tone' of the model"
            "Text" => model.systemPrompt
        }        
