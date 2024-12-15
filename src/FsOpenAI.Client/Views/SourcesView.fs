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

type ModelQueryView() =
    inherit ElmishComponent<Model,Message>()

    override this.View mdl dispatch =
        let mode = Model.selectedChat mdl |> Option.map (fun x -> x.Mode) |> Option.defaultValue M_Plain
        let useWeb = Model.selectedChat mdl |> Option.map (fun x -> Interaction.useWeb x) |> Option.defaultValue false

        comp<RadzenStack> {
            "Orientation" => Orientation.Vertical
            "AlignItems" => AlignItems.Start
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "AlignItems" => AlignItems.Center
                comp<RadzenCheckBox<bool>> {
                    attr.title "Query AI model directly (no index search)"
                    "Value" => (mode = M_Plain)
                    attr.callback "Change" (fun (v:bool) ->
                        Model.selectedChat mdl |> Option.iter (fun chat ->
                            dispatch (Ia_UseWeb (chat.Id, Interaction.useWeb chat))))
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
                    "Value" => ((mode = M_Plain) && useWeb)
                    attr.callback "Change" (fun (v:bool) ->
                        Model.selectedChat mdl |> Option.iter (fun chat ->
                            dispatch (Ia_UseWeb (chat.Id, v))))
                }
                comp<RadzenLabel> {
                    "Text" => "With web search"
                }
            }
        }

type IndexTreeView() =
    inherit ElmishComponent<Model,Message>()

    member val IndexTree =  Ref<RadzenTree>() with get, set
    //let indexTree = Ref<RadzenTree>()

    member this.CheckedValuesChanged (xs:obj seq) =
        let idxRefs = xs |> Seq.cast<IndexTree> |> Seq.map (fun x -> x.Idx) |> List.ofSeq
        Model.selectedChat this.Model
        |> Option.iter (fun chat ->
            this.Dispatch (Ia_SetIndex (chat.Id, idxRefs)))

    member this.Change (x:TreeEventArgs) =        
        let xs = this.IndexTree.Value |> Option.map (fun x -> x.CheckedValues) |> Option.defaultValue Seq.empty 
        let idxRefs = xs |> Seq.cast<IndexTree> |> Seq.map (fun x -> x.Idx) |> List.ofSeq
        Model.selectedChat this.Model
        |> Option.iter (fun chat ->
            this.Dispatch (Ia_SetIndex (chat.Id, idxRefs)))

    member this.ValueChanged (_:obj) =
        let xs = this.IndexTree.Value |> Option.map (fun x -> x.CheckedValues) |> Option.defaultValue Seq.empty 
        let idxRefs = xs |> Seq.cast<IndexTree> |> Seq.map (fun x -> x.Idx) |> List.ofSeq
        Model.selectedChat this.Model
        |> Option.iter (fun chat ->
            this.Dispatch (Ia_SetIndex (chat.Id, idxRefs)))

    member this.CheckedValues with get() =
        let idxs =

            Model.selectedChat this.Model
            |> Option.bind (fun chat -> Interaction.qaBag chat |> Option.map (fun bag -> chat.Mode,bag.Indexes))
            |> Option.map (fun (mode,idxs) ->
                match mode with
                | M_Index
                | M_Doc_Index -> IO.selectIndexTrees (set idxs) this.Model.indexTrees
                | _           -> Set.empty)
            |> Option.defaultValue Set.empty
        //printfn "CheckedValues: %A" (idxs |> Set.map (fun x -> x.Idx))
        idxs

    override this.View model dispatch =
        if model.indexTrees.IsEmpty then
            comp<RadzenText> {
                "Text" => "No indexes available"
            }
        else
            comp<RadzenTree> {
                "AllowCheckBoxes" => true
                "AllowCheckChildren" => true
                "AllowCheckParents" => true
                "Data" => model.indexTrees
                "CheckedValues" => this.CheckedValues
                //attr.callback "ValueChanged" this.ValueChanged
                //attr.callback "Change" this.Change
                attr.callback "CheckedValuesChanged" this.CheckedValuesChanged
                this.IndexTree
                comp<RadzenTreeLevel> {
                    "TextProperty" => "Description"
                    "ChildrenProperty" => "Children"
                    "Expanded" => Func<_,_>(fun (t:obj) -> true)
                }
            }

type SourcesView() =
    inherit ElmishComponent<Model,Message>()

    let makeWrapped wrap title (content:Node) =
        if wrap then
            comp<RadzenFieldset> {
                "Text" => title
                content
            }
        else
            content

    override this.View model dispatch =
        let haveDoc = Model.selectedChat model |> Option.bind Interaction.docContent |> Option.bind (fun x -> match x.Status with | No_Document -> None | _ -> Some x)
        let showPlain = if Model.isEnabled M_Plain model then 1 else 0
        let showIndex = if Model.isEnabledAny [M_Doc_Index; M_Index] model then 1 else 0
        let showDoc = if Model.isEnabledAny [M_Doc_Index; M_Doc] model && haveDoc.IsSome then 1 else 0
        let showCode = if Model.isEnabled M_CodeEval model then 1 else 0
        let wrap = (showPlain + showIndex + showDoc + showCode) > 1

        comp<RadzenColumn> {
            attr.``class`` "rz-p-1 rz-ml-2"
            comp<RadzenRow> {
                comp<RadzenText> {
                    "Text" => "Sources"
                    "TextStyle" => TextStyle.H6
                }
            }
            comp<RadzenRow> {
                comp<RadzenStack> {
                    concat {
                        if showPlain > 0 then
                            makeWrapped wrap "Model" (ecomp<ModelQueryView,_,_> model dispatch {attr.empty()})
                        if showDoc > 0 then
                            makeWrapped wrap "Document" (ecomp<DocView,_,_> model dispatch {attr.empty()})
                        if showIndex > 0 then
                            makeWrapped wrap "Indexes" (ecomp<IndexTreeView,_,_> model dispatch {attr.empty()})
                        if showCode > 0 then
                            makeWrapped wrap "Code" (ecomp<CodeEvalView,_,_> model dispatch {attr.empty()})
                    }
                }
            }
        }

