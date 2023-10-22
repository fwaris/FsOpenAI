namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open System.Linq.Expressions


type QAView() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    override this.View m dispatch =
        let bag,chat,model = m
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
            ecomp<ChatHistoryView,_,_> (chat,model) dispatch { attr.empty() }
        }
