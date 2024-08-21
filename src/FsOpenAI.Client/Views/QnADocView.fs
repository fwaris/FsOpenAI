namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open Microsoft.AspNetCore.Components.Forms


type QnADocView() =
    inherit ElmishComponent<DocumentContent*Interaction*Model,Message>()
    
    let inputFile = Ref<MudFileUpload<IBrowserFile>>()   

    override this.View m dispatch =
        let docCntnt,chat,model = m        
        comp<MudPaper> {            
            "Elevation" => 3
            "Class" => "d-flex flex-column ma-2"
            comp<MudPaper> {
                "Class" => "d-flex flex-row flex-grow-1"
                "Elevation" => 0
                //ecomp<ChatSettingsView,_,_> (chat,model) dispatch {attr.empty()}
                ecomp<SystemMessageShortView,_,_> (chat,model) dispatch {attr.empty()}
                ecomp<DocSelectorView,_,_> (docCntnt,chat,model) dispatch {attr.empty()}
                //comp<MudPaper> {
                //    "Class" => "d-block d-flex flex-1 ma-2"
                //}
            }
        }