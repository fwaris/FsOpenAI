namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open System.Collections.Generic
open FsOpenAI.Shared

type SourceTree = {
    Id: string
    Description: string
    Children: SourceTree list
}

type DocView() = 
    inherit ElmishComponent<DocumentContent*Interaction*Model,Message>()

    override this.View mdl dispatch =
        let bag,chat,model = mdl

        let title = 
            match bag.Status with 
            | No_Document -> "No document selected"
            | Uploading -> "Uploading document..."
            | Receiving -> "Receiving document content ..."
            | ExtractingTerms -> "Extracting search terms..."
            | Ready -> "Document ready"

        let icon = 
            match bag.Status with 
            | No_Document -> "upload_file" //Icons.Material.Outlined.UploadFile
            | Uploading -> "upload" //Icons.Material.Outlined.Upload
            | Receiving -> "download" //Icons.Material.Outlined.Download
            | ExtractingTerms -> "content_paste_search" //Icons.Material.Outlined.ContentPasteSearch
            | Ready -> "check" // Icons.Material.Outlined.Check
        
        let color = 
            match bag.Status with 
            | Ready       -> Colors.Success
            | No_Document -> Colors.Info
            | _           -> Colors.Warning
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "AlignItems" => Align.Center
            comp<RadzenButton> {
                "Icon" => icon
                attr.title title
                "IconColor" => color
            }
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "AlignItems" => AlignItems.Center
                comp<RadzenCheckBox<bool>> {
                    "Value" => true
                }
                comp<RadzenLabel> {
                    "Text" => (IO.browserFile bag.DocumentRef |> Option.map (fun x -> x.Name) |> Option.defaultValue "No document selected")
                }
            }
        }



type IndexTreeView() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    override this.View mdl dispatch =
        let bag,chat,model = mdl

        let webSearch = 
            model.serviceParameters
            |> Option.bind (fun x -> x.BING_ENDPOINT)
            |> Option.map (fun x -> [ {Id="1.1"; Description="With web search"; Children=[]} ])
            |> Option.defaultValue []
        
        let modelSource = 
            model.appConfig.EnabledChatModes 
            |> List.tryFind (fun (x,_) -> x = ChatMode.CM_Plain)
            |> Option.map (fun _ -> [ {Id="1.0"; Description="AI Model"; Children=webSearch} ])
            |> Option.defaultValue []

        let doc = Interaction.docContent chat

        let checkedVals = IO.selectIndexTrees (set bag.Indexes) model.indexTrees
        comp<RadzenColumn> {
            attr.``class`` "rz-p-1 rz-ml-2"
            comp<RadzenRow> {
                comp<RadzenText> {
                    "Text" => "Sources"
                    "TextStyle" => TextStyle.H6
                }
            }
            comp<RadzenRow> {
                comp<RadzenCard> {                                    
                    comp<RadzenStack> {
                        match modelSource with
                        | [] -> ()
                        | ts -> 
                            comp<RadzenTree> {
                                "AllowCheckBoxes" => true
                                "Data" => ts
                                //"CheckedValues" => checkedVals
                                // attr.callback "CheckedValuesChanged" (fun (xs:obj seq) -> 
                                //     let idxRefs = xs |> Seq.cast<IndexTree> |> Seq.map (fun x -> x.Idx) |> List.ofSeq
                                //     dispatch (Ia_SetIndex (chat.Id, idxRefs)))
                                comp<RadzenTreeLevel> {
                                    "TextProperty" => "Description"
                                    "ChildrenProperty" => "Children"
                                    "Expanded" => Func<_,_>(fun (t:obj) -> true) 
                                }                                            
                            }
                        match doc with 
                        | None -> ()
                        | Some doc -> 
                            comp<RadzenFieldset> {
                                "Text" => "Document"
                                ecomp<DocView,_,_> (doc,chat,model) dispatch {attr.empty()}
                            }
                        comp<RadzenFieldset> {
                            "Text" => "Indexes"
                            comp<RadzenTree> {
                                "AllowCheckBoxes" => true
                                "Data" => model.indexTrees
                                "CheckedValues" => checkedVals
                                attr.callback "CheckedValuesChanged" (fun (xs:obj seq) -> 
                                    let idxRefs = xs |> Seq.cast<IndexTree> |> Seq.map (fun x -> x.Idx) |> List.ofSeq
                                    dispatch (Ia_SetIndex (chat.Id, idxRefs)))
                                comp<RadzenTreeLevel> {
                                    "TextProperty" => "Description"
                                    "ChildrenProperty" => "Children"
                                    "Expanded" => Func<_,_>(fun (t:obj) -> true) 
                                }                                            
                            }
                        }

                    }
                }
            }
        }

(*

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
        //let isels = (Set.empty,bag.Indexes |> List.map (fun x->treeMap.[x])) ||> List.fold IO.subTree
        let isels =
            let found = bag.Indexes |> List.choose (fun x -> Map.tryFind x treeMap)
            (Set.empty,found) ||> List.fold IO.subTree
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
                "Items" => HashSet<TreeItemData<IndexTree>> (model.indexTrees |> List.map(fun x -> TreeItemData<IndexTree>(Value=x)))
                "SelectionMode" => SelectionMode.MultiSelection
                "Dense" => true
                attr.callback "SelectedValuesChanged" (fun (xs:IReadOnlyCollection<IndexTree>) -> sels <- HashSet(xs))
                attr.fragmentWith "ItemTemplate"(fun (idxt:TreeItemData<IndexTree>) ->
                    comp<MudTreeViewItem<IndexTree>> {
                        "Value" => idxt.Value
                        "Selected" => sels.Contains idxt.Value
                        "Items" => HashSet<TreeItemData<IndexTree>>(idxt.Value.Children |> List.map(fun x -> TreeItemData<IndexTree>(Value=x)))
                        "Expanded" => true
                        attr.fragmentWith "BodyContent" (fun (_:MudTreeViewItem<IndexTree>) ->
                            comp<MudPaper> {
                                "Class" => "align-center"
                                "Elevation" => 0
                                text idxt.Value.Description
                                comp<MudChip<string>> {
                                   // text idxt.Idx.Name
                                    text idxt.Value.Tag
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
                        comp<MudChip<string>> {
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

*)
