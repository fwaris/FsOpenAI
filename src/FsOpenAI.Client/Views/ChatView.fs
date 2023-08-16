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
        comp<MudPaper> {
            "Class" => "mt2"            
            div {
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
            }
            ecomp<ChatHistoryView,_,_> m dispatch {attr.empty()}
        }


