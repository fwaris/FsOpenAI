namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions

type PlainView() =
    inherit ElmishComponent<Interaction*Model,Message>()

    override this.View m dispatch =
        let chat,model = m
        let cbag = Interaction.cBag chat
        comp<MudPaper> {
            "Elevation" => 3
            "class" => "d-flex ma-2"
            //ecomp<ChatSettingsView,_,_> (chat,model) dispatch {attr.empty()}
            comp<MudPaper> {
                "Class" => "d-flex flex-1 ma-2 align-self-center "
                ecomp<SystemMessageView,_,_> chat dispatch {attr.empty()}
            }
            if model.serviceParameters.Value.BING_ENDPOINT.IsSome then 
                comp<MudPaper> {
                    "Class" => "d-flex flex-none align-self-center ma-2"
                    comp<MudTooltip> {
                        "Delay" => 100.0
                        "Text" => "Use Bing search to augment"
                        comp<MudCheckBox<bool>> { 
                            "Class" => "mr-2"
                            "Label" => "Use web"    
                            "Value" => cbag.UseWeb
                            attr.callback "ValueChanged" (fun (t:bool) -> dispatch (Ia_UseWeb (chat.Id,t)))
                        }
                    }
                }
        }

