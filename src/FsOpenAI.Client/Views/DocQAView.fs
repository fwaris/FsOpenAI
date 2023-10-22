namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open System.Linq.Expressions
open Microsoft.AspNetCore.Components.Forms


type DocQAView() =
    inherit ElmishComponent<DocBag*Interaction*Model,Message>()
    
    let inputFile = Ref<MudFileUpload<IBrowserFile>>()   

    override this.View m dispatch =
        let dbag,chat,model = m
        let bag = dbag.QABag
        comp<MudPaper> {            
            comp<MudPaper> {
                "Class" => "d-block d-flex flex-grow-1 ma-2"
                ecomp<ChatSettingsView,_,_> (chat,model) dispatch {attr.empty()}
                ecomp<SystemMessageShortView,_,_> (chat,model) dispatch {attr.empty()}
                comp<MudPaper> {
                    "Class" => "d-flex flex-1 ma-2"
                    ecomp<IndexSelectionView,_,_> (bag,chat,model) dispatch {attr.empty()}
                }
            }
            comp<MudPaper> {
                "Class" => "d-block d-flex flex-1 ma-2"
                ecomp<DocumentView,_,_> (dbag,chat,model) dispatch {attr.empty()}
            }
            comp<MudPaper> {
                "Class" => "d-block d-flex flex-1 ma-2"
                ecomp<PromptSelectorView,_,_> (DocQuery,dbag,chat,model) dispatch {attr.empty()}
            }
            ecomp<ChatHistoryView,_,_> (chat,model) dispatch { attr.empty() }
        }
