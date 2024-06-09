namespace FsOpenAI.Client.Views
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open System.Collections.Generic
open FsOpenAI.Shared

type IndexTreePopup() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    let tree = Ref<MudTreeView<IndexTree>>()
    let mutable sels = HashSet<IndexTree>()

    let setIndexes id treeMap dispatch =         
        let isels = sels |> Seq.map (fun x -> x.Idx) |> set
        let psels = 
            (isels,isels) 
            ||> Set.fold(fun acc idx -> 
                let subT = treeMap |> Map.find idx |> IO.subTree Set.empty |> Set.map (fun x->x.Idx)
                let childNodes = Set.remove idx subT
                (acc,childNodes) ||> Set.fold (fun acc x -> acc |> Set.remove x))
        dispatch (Ia_SetIndex (id,Set.toList psels))

    override this.View m dispatch =
        let bag,chat,model = m        
        let treeMap = Init.flatten model.indexTrees |> List.map(fun x -> x.Idx,x) |> Map.ofList                
        let isels = (Set.empty,bag.Indexes |> List.map (fun x->treeMap.[x])) ||> List.fold IO.subTree
        sels.Clear()
        isels |> Seq.iter(fun x -> sels.Add x |> ignore)

        comp<MudPopover> {
            "Open" => TmpState.isIndexOpen chat.Id model
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
                on.click(fun _ -> setIndexes chat.Id treeMap dispatch)
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
