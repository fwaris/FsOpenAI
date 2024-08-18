namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared
open Radzen
open Radzen.Blazor
open Microsoft.AspNetCore.Components

type SidebarView() =
    inherit ElmishComponent<Model,Message>()

    override this.View model dispatch =
        comp<RadzenSidebar> {
            "Expanded" => TmpState.isOpen C.SIDE_BAR_EXPANDED model
            attr.callback "ExpandedChanged" (fun (b:bool) -> dispatch ToggleSideBar)
            comp<RadzenDataList<string>> {
                "Data" => ["a"; "b"; "c"]
                attr.fragmentWith "Template" (fun (x:string) ->
                    comp<RadzenText> {
                        "Text" => x
                    })
            }
        }
