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

type QAView() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    override this.View m dispatch =
        let bag,chat,model = m
        let settingsOpen = model.settingsOpen |> Map.tryFind chat.Id |> Option.defaultValue false
        let panelId = C.CHAT_DOCS chat.Id
        let isPanelOpen = model.settingsOpen |> Map.tryFind panelId |> Option.defaultValue false
        comp<MudContainer> {
            "Class" => "mud-height-full"            
            div {
                "class" => "d-flex flex-grow-1 gap-1"
                comp<MudPaper> {
                    "Class" => "d-flex flex-none align-self-start mt-4"
                    ecomp<ChatParametersView,_,_> (settingsOpen,chat,model) dispatch {attr.empty()}
                }
                comp<MudPaper> {
                    "Class" => "d-flex flex-1 ma-3"
                    ecomp<IndexSelectionView,_,_> (bag,chat,model) dispatch {attr.empty()}
                }
                comp<MudPaper> {
                    "Class" => "d-flex flex-none ma-3"
                    comp<MudIconButton> {
                        "Icon" => Icons.Material.Outlined.Folder
                        "Disabled" => bag.Documents.IsEmpty
                        on.click (fun _ -> dispatch (OpenCloseSettings panelId))
                    }
                }
            }
            ecomp<ChatHistoryView,_,_> (chat,model) dispatch { attr.empty() }
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
        }
