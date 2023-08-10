namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open System.Linq.Expressions

type SVAttr =
    static member Property(expr:Expression<Func<Document,string>>) =
        "Property" => expr

type SearchResultsView() =
    inherit ElmishComponent<string*bool*QABag,Message>()    

    override this.View model dispatch =
        let panelId,isPanelOpen,bag = model
        comp<MudPopover> {
            "Style" => "width:75%; height:500px;"
            "AnchorOrigin" => Origin.TopRight
            "TransformOrigin" => Origin.TopRight
            "Open" => isPanelOpen
            comp<MudTable<Document>> {
                "Items" => bag.Documents
                attr.fragment "ToolBarContent" (
                    concat {
                        comp<MudText> {"Typo" => Typo.h6; "Search Results"}
                        comp<MudSpacer> {attr.empty()}
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Filled.Close
                            on.click(fun _ -> dispatch (OpenCloseSettings panelId))
                        }
                    }
                )
                attr.fragmentWith "RowTemplate" (fun (o:Document) ->
                    comp<MudStack> {
                        "Class" => "ma-4"
                        comp<MudLink> {                            
                            "Href" => o.Ref
                            "Target" => "_blank"
                            o.Ref
                        }
                        comp<MudText> {
                            "Style" => "height:400px"
                            text o.Text
                        }
                    }
                )
                attr.fragment "PagerContent" (
                    comp<MudTablePager> {
                        "HideRowsPerPage" => true
                        "PageSizeOptions" => [|1|]
                    }
                )
            }
        }
