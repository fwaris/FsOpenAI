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
        let settingsOpen = model.settingsOpen |> Map.tryFind chat.Id |> Option.defaultValue false
        let panelId = C.CHAT_DOCS chat.Id
        let isPanelOpen = model.settingsOpen |> Map.tryFind panelId |> Option.defaultValue false
        comp<MudPaper> {            
            comp<MudPaper> {
                "class" => "d-flex flex-grow-1 gap-1"
                comp<MudPaper> {
                    "Class" => "d-flex flex-none align-self-start mt-5"
                    comp<MudIconButton> { 
                        "Icon" => Icons.Material.Outlined.Settings
                        on.click(fun e -> dispatch (OpenCloseSettings chat.Id))
                    }
                    ecomp<ChatParametersView,_,_> (settingsOpen,chat,model) dispatch {attr.empty()}
                }
                comp<MudPaper> {
                    "Class" => "d-flex flex-1 mt-3"
                    ecomp<SysPromptView,_,_> chat dispatch {attr.empty()}
                }
                comp<MudPaper> {
                    "Class" => "d-flex flex-1 ma-3"
                    ecomp<IndexSelectionView,_,_> (bag,chat,model) dispatch {attr.empty()}
                }
                comp<MudPaper> {
                    "Class" => "d-flex flex-none align-self-start mt-5"
                    comp<MudTooltip> {
                        "Text" => "View search results"
                        "Arrow" => true
                        "Delayed" => 200.0
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Outlined.Folder
                            "Disabled" => bag.Documents.IsEmpty
                            on.click (fun _ -> dispatch (OpenCloseSettings panelId))
                        }
                    }
                }
            }
            ecomp<ChatHistoryView,_,_> (chat,model) dispatch { attr.empty() }
            ecomp<SearchResultsView,_,_> (panelId,isPanelOpen,bag) dispatch {attr.empty()}
        }
