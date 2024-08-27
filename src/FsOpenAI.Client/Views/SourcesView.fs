namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open System.Collections.Generic
open FsOpenAI.Shared

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
            "AlignItems" => AlignItems.Center
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

type ModelQueryView() = 
    inherit ElmishComponent<Interaction*Model,Message>()

    override this.View mdl dispatch =
        let chat,model = mdl
        
        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "AlignItems" => AlignItems.Start
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "AlignItems" => AlignItems.Center
                comp<RadzenCheckBox<bool>> {
                    attr.title "Query AI model directly (no index search)"
                    "Value" => (chat.Mode = M_Plain)
                    attr.callback "Change" (fun (v:bool) -> 
                        dispatch (Ia_UseWeb (chat.Id, Interaction.useWeb chat)))
                }
                comp<RadzenLabel> {
                    "Text" => "AI Model"
                }
            }
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "AlignItems" => AlignItems.Center
                attr.``class`` "rz-ml-2"
                comp<RadzenCheckBox<bool>> {
                    attr.title "Include web search results to enrich answer"
                    "Value" => ((chat.Mode = M_Plain) && Interaction.useWeb chat)
                    attr.callback "Change" (fun (v:bool) -> 
                        dispatch (Ia_UseWeb (chat.Id, v)))
                }
                comp<RadzenLabel> {
                    "Text" => "With web search"
                }
            }
        }
        
        

type IndexTreeView() = 
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    override this.View mdl dispatch =
        let bag,chat,model = mdl

        let checkedVals = 
            match chat.Mode with
            | M_Index 
            | M_Doc_Index -> IO.selectIndexTrees (set bag.Indexes) model.indexTrees
            | _           -> Set.empty

        comp<RadzenTree> {
            "AllowCheckBoxes" => true
            "AllowCheckChildren" => true
            "AllowCheckParents" => true
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

type SourcesView() =
    inherit ElmishComponent<Interaction*Model,Message>()

    let makeWrapped wrap title (content:Node) = 
        if wrap then
            comp<RadzenFieldset> {
                "Text" => title
                content
            }
        else
            content

    override this.View mdl dispatch =
        let chat,model = mdl

        let qaBag = 
            Interaction.qaBag chat 
                |> Option.orElseWith (fun _ -> 
                    if Model.isEnabledAny [M_Index; M_Doc_Index] model then 
                        Some QABag.Default
                    else
                        None)

        let doc = Interaction.docContent chat
        
        let showPlain = if Model.isEnabled M_Plain model then 1 else 0
        let showIndex = if qaBag.IsSome then 1 else 0
        let showDoc = if doc.IsSome then 1 else 0
        let wrap = (showPlain + showIndex + showDoc) > 1

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
                        concat {
                            if showPlain > 0 then
                                makeWrapped wrap "Model" (ecomp<ModelQueryView,_,_> (chat,model) dispatch {attr.empty()})
                            if showDoc > 0 then
                                match doc with 
                                | None -> ()
                                | Some doc -> makeWrapped wrap "Document" (ecomp<DocView,_,_> (doc,chat,model) dispatch {attr.empty()})
                            if showIndex > 0 then
                                match qaBag with
                                | None -> ()
                                | Some bag -> makeWrapped wrap "Indexes" (ecomp<IndexTreeView,_,_> (bag,chat,model) dispatch {attr.empty()})
                        }
                    }
                }
            }
        }

