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
    inherit ElmishComponent<string*bool*Document list,Message>()    

    override this.View model dispatch =
        let panelId,isPanelOpen,docs = model
        comp<MudPopover> {
            "Style" => "width:75%;max-height:500px;"
            "AnchorOrigin" => Origin.TopRight
            "TransformOrigin" => Origin.TopRight
            "Open" => isPanelOpen
            comp<MudPaper> {
                "Class" => "border-solid border mud-border-warning rounded-lg ma-2 pa-2"
                comp<MudTable<Document>> {
                    "Items" => docs
                    attr.fragment "ToolBarContent" (
                        concat {
                            comp<MudText> {"Typo" => Typo.subtitle2; "Search Results"}
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
                                o.Title
                            }
                            comp<MudText> {
                                "Style" => "height:14.0rem;"
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
        }
