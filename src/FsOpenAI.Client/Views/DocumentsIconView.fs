namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open System.Linq.Expressions

type DocumentsIconView() =
    inherit ElmishComponent<string*Document list,Message>()    

    override this.View model dispatch =
        let panelId,docs = model
        comp<MudTooltip> {
            "Text" => "View search results"
            "Arrow" => true
            "Delayed" => 200.0
            comp<MudIconButton> {
                "Class" => "d-flex flex-none align-self-center ma-2"
                "Icon" => if docs.IsEmpty then Icons.Material.Outlined.Folder else Icons.Material.Outlined.FolderOpen
                "Color" => if docs.IsEmpty then Color.Default else Color.Warning
                "Disabled" => docs.IsEmpty
                on.click (fun _ -> dispatch (OpenCloseSettings panelId))
            }
        }
