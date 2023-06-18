module AppSettingsView
open System
open Bolero
open FsOpenAI.Client.Model
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

let dnld(uri:string) =
    async {
        use wc = new HttpClient()
        let! str = wc.GetStringAsync(uri) |> Async.AwaitTask
        return str
    }
    

