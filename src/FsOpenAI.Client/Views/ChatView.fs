namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client

type ChatView() =
    inherit ElmishComponent<Interaction*Model,Message>()

    override this.View m dispatch =
        let chat,model = m
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
                    ecomp<SysPromptView,_,_> chat dispatch {attr.empty()}
                }
            }
            ecomp<ChatHistoryView,_,_> m dispatch {attr.empty()}
        }


