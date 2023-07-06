namespace FsOpenAI.Client.Views
open System
open Bolero
open FsOpenAI.Client
open Bolero.Html
open MudBlazor
open System.Net.Http

type SysPromptView() =
    inherit ElmishComponent<Model,Message>()
    
    override this.View model dispatch =
        comp<MudPaper> {
            comp<MudForm> {
                concat {
                    comp<MudTextField<string>> {
                        text "pass"
                    }
                }
            }
}    

