namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Client.Interactions

type IndexSelectionView() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    let dispatchSel id bag dispatch (xs:IndexRef seq)=
        xs 
        |> Seq.toList
        |> fun idxs -> dispatch (Ia_UpdateQaBag (id,{bag with Indexes=idxs}))
            
    override this.View mdl dispatch =
        let bag,chat,model = mdl
        let panelId = C.CHAT_DOCS chat.Id
        let isPanelOpen = model.settingsOpen |> Map.tryFind panelId |> Option.defaultValue false
        let docs = Interaction.getDocuments chat
        comp<MudPaper> {
            "Class" => "d-flex flex-1"
            comp<MudSelect<IndexRef>> {
                "Class" => "d-flex flex-1"
                "Label" => "Available Document Indexes"
                "MultiSelection" => true
                "Variant" => Variant.Outlined
                "SelectedValues" => bag.Indexes 
                attr.callback "SelectedValuesChanged" (dispatchSel chat.Id bag dispatch)
                "Label" => "Index Name"
                "ToStringFunc" => (System.Func<IndexRef,string>(fun (x:IndexRef) -> match x with Azure n ->n.Name))
                for ir in model.indexRefs do
                    comp<MudSelectItem<IndexRef>>{
                        "Value" => ir
                        match ir with Azure n -> $"{n.Description} || {n.Name}"
                    }                                    
            }           
            comp<MudIconButton> {
                "Class" => "d-flex flex-none align-self-center ma-2"
                "Icon" => Icons.Material.Outlined.Refresh
                on.click(fun _ -> dispatch (RefreshIndexes false))
            }            
            ecomp<DocumentsIconView,_,_> (panelId,docs) dispatch {attr.empty()}
            ecomp<SearchResultsView,_,_> (panelId,isPanelOpen,docs) dispatch {attr.empty()}
        }

