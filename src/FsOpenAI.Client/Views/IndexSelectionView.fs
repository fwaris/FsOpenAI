namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client

type IndexSelectionView() =
    inherit ElmishComponent<QABag*Interaction*Model,Message>()

    let dispatchSel id bag dispatch (xs:IndexRef seq)=
        xs 
        |> Seq.toList
        |> fun idxs -> dispatch (Ia_UpdateQaBag (id,{bag with Indexes=idxs}))
            
    override this.View mdl dispatch =
        let bag,chat,model = mdl
        comp<MudPaper> {
            "Class" => "d-flex flex-1"
            comp<MudPaper> {
                "Class" => "d-flex flex-1"
                comp<MudSelect<IndexRef>> {
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
                            match ir with Azure n -> n.Name
                        }                                    
                }           
            }
            comp<MudPaper> {
                "Class" => "d-flex flex-none align-self-start mt-2"
                comp<MudIconButton> {
                    "Icon" => Icons.Material.Outlined.Refresh
                    on.click(fun _ -> dispatch (RefreshIndexes false))
                }                
            }
        }

