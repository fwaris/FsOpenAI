namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client

type QAView() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    override this.View m dispatch =
        let bag,chat,model = m
        let settingsOpen = model.settingsOpen |> Map.tryFind chat.Id |> Option.defaultValue false
        comp<MudContainer> {
            "Class" => "mt2"            
            div {
                "class" => "d-flex flex-grow-1 gap-1"
                comp<MudPaper> {
                    "Class" => "d-flex flex-none align-self-start mt-4"
                    ecomp<ChatParametersView,_,_> (settingsOpen,chat) dispatch {attr.empty()}
                }
                comp<MudPaper> {
                    "Class" => "d-flex flex-1 ma-3"
                    ecomp<IndexSelectionView,_,_> (bag,chat,model) dispatch {attr.empty()}
                }
            }
            ecomp<ChatHistoryView,_,_> (chat,model) dispatch {attr.empty()}
        }


