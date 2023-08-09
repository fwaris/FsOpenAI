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
            "Class" => "mud-height-full"
            "Style" => "width:500px"
            "AnchorOrigin" => Origin.TopRight
            "TransformOrigin" => Origin.TopRight
            "Open" => isPanelOpen
            comp<MudDataGrid<Document>> {
                "Striped" => true
                "Items" => bag.Documents
                "RowsPerPage" => 1
                "Dense" => true
                "Height" => "600px"
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
                attr.fragment "Columns" (
                    concat {
                        comp<PropertyColumn<Document,string>> {
                            SVAttr.Property (fun d->d.Text)
                            "Title" => "Text"
                        }
                        comp<PropertyColumn<Document,string>> {
                            SVAttr.Property (fun d->d.Ref)
                            "Title" => "Ref"
                        }
                    }
                )
                attr.fragment "PagerContent" (
                    comp<MudDataGridPager<Document>> {attr.empty()}
                )
            }            
        }
