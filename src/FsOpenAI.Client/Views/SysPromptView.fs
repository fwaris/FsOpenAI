namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client

type SysPromptView() =
    inherit ElmishComponent<Chat,Message>()
    
    override this.View chat dispatch =
        comp<MudTextField<string>> {              
            attr.callback "ValueChanged" (fun e -> dispatch (Chat_SysPrompt (chat.Id,e)))
            "Variant" => Variant.Outlined
            "Label" => "System Prompt"
            "Lines" => 10
            "Placeholder" => "Set the 'tone' of the model"
            "Text" => chat.Id
        }        
