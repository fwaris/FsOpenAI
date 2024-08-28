namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared


type IndexQnAView() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    override this.View m dispatch =
        let bag,chat,model = m
        comp<MudPaper> {
            "Elevation" => 3
            "Class" => "d-flex ma-2"
            //ecomp<ChatSettingsView,_,_> (chat,model) dispatch {attr.empty()}
            ecomp<SystemMessageShortView,_,_> (chat,model) dispatch {attr.empty()}
            comp<MudPaper> {
                "Class" => "d-flex flex-1 ma-2"
                //ecomp<IndexSelectionView,_,_> (bag,chat,model) dispatch {attr.empty()}
                //ecomp<SourcesView,_,_> (chat,model) dispatch {attr.empty()}
            }
        }
