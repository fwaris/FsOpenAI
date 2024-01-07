namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Client.Interactions
open System.Collections.Generic

type IndexTreePopup() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    let tree = Ref<MudTreeView<IndexTree>>()
    let mutable sels = HashSet<IndexTree>()

    let setIndexes id dispatch =         
        let sels = sels |> Seq.map (fun x -> x.Idx) |> Seq.toList |> List.distinct        
        dispatch (Ia_SetIndex (id,sels))

    override this.View m dispatch =
        let bag,chat,model = m        
        let treeMap = Init.flatten model.indexTrees |> List.map(fun x -> x.Idx,x) |> Map.ofList        
        bag.Indexes
        |> Seq.choose (fun idx -> treeMap |> Map.tryFind idx)
        |> Seq.iter (fun idx -> sels.Add idx |> ignore)

        comp<MudPopover> {
            "Open" => Update.TmpState.isIndexOpen chat.Id model
            "AnchorOrigin" => Origin.TopRight
            "TransformOrigin" => Origin.TopRight
            "Paper" => true
            "Class" => "d-flex flex-row flex-grow-1 mud-full-width"        
            "Style" => "max-width:90vw; overflow: auto;"
            comp<MudTreeView<IndexTree>> {
                "Class" => "d-flex flex-grow-1 flex-column"
                "Items" => HashSet model.indexTrees
                "MultiSelection" => true
                "Dense" => true
                attr.callback "SelectedValuesChanged" (fun (xs:HashSet<IndexTree>) -> sels <- xs)
                attr.fragmentWith "ItemTemplate"(fun (idxt:IndexTree) -> 
                    comp<MudTreeViewItem<IndexTree>> {
                        "Value" => idxt
                        "Selected" => sels.Contains idxt
                        "Items" => HashSet idxt.Children 
                        "Expanded" => true
                        attr.fragmentWith "BodyContent" (fun (_:MudTreeViewItem<IndexTree>) -> 
                            comp<MudPaper> {
                                "Class" => "align-center"
                                "Elevation" => 0
                                text idxt.Description
                                comp<MudChip> {
                                    text idxt.Idx.Name
                                }
                            }
                        )
                    }
                )
                tree
            }
            comp<MudIconButton> {
                "Class" => "align-self-start ma-2"
                "Icon" => Icons.Material.Outlined.ExpandLess
                on.click(fun _ -> setIndexes chat.Id dispatch)
            }
        }

type IndexTreeView() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()
            
    override this.View mdl dispatch =
        let bag,chat,model = mdl
        comp<MudPaper> {
            "Class" => "d-flex flex-1 flex-row align-center"
            comp<MudField> {
                "Class" => "ma-1 self-align-stretch pa-1"
                "Variant" => Variant.Outlined
                "Label" => "Indexes"
                "InnerPadding" => false
                comp<MudPaper> {
                    "Style" => "height: 2.5rem; overflow-y: auto;"
                    "Elevation" => 0
                    for idx in bag.Indexes do                        
                        comp<MudChip> {
                            text idx.Name
                        }
                }
            }
            comp<MudIconButton> {
                "Class" => "ma-2 self-align-end"
                "Icon" => Icons.Material.Outlined.ExpandMore
                "Title" => "Select indexes"
                on.click(fun _ -> dispatch (Ia_OpenIndex chat.Id))
            }
            ecomp<IndexTreePopup,_,_> mdl dispatch {attr.empty()}
        }
