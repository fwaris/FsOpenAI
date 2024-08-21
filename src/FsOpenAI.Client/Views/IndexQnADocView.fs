namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components.Forms
open FsOpenAI.Shared


type IndexQnADocView() =
    inherit ElmishComponent<DocBag*Interaction*Model,Message>()
    
    let inputFile = Ref<MudFileUpload<IBrowserFile>>()   

    override this.View m dispatch =
        let dbag,chat,model = m
        let bag = dbag.QABag
        comp<MudPaper> {            
            "Elevation" => 3
            "Class" => "d-flex flex-column ma-2"
            comp<MudPaper> {
                "Class" => "d-flex flex-row flex-grow-1 ma-2"
                //ecomp<ChatSettingsView,_,_> (chat,model) dispatch {attr.empty()}
                ecomp<SystemMessageShortView,_,_> (chat,model) dispatch {attr.empty()}
                comp<MudPaper> {
                    "Class" => "d-flex flex-1 ma-2"
                    ecomp<IndexTreeView,_,_> (bag,chat,model) dispatch {attr.empty()}
                }
            }
            comp<MudPaper> {
                "Class" => "d-flex flex-row overflow-hidden"
                "Style" => "height:5rem"
                comp<MudPaper> {
                    "Class" => "d-block d-flex flex-1 ma-2"
                    ecomp<DocBagView,_,_> (dbag,chat,model) dispatch {attr.empty()}
                }
                comp<MudPaper> {
                    "Class" => "d-block d-flex flex-1 ma-2"
                    ecomp<PromptSelectorView,_,_> (DocQuery,dbag,chat,model) dispatch {attr.empty()}
                }
            }
        }