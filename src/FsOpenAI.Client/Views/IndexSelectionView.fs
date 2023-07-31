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
        |> Seq.tryHead
        |> fun idx -> dispatch (Ia_UpdateQaBag (id,{bag with Index=idx}))
            
    override this.View mdl dispatch =
        let bag,chat,model = mdl
        comp<MudPaper> {
            "Class" => "d-flex flex-1"
            comp<MudPaper> {
                "Class" => "d-flex flex-1"
                comp<MudSelect<IndexRef>> {
                    "Label" => "Available Document Indexes"
                    "Variant" => Variant.Outlined
                    "SelectedValues" => match bag.Index with Some ir -> [ir] | _ -> []
                    attr.callback "SelectedValuesChanged" (dispatchSel chat.Id bag dispatch)
                    "Label" => "Index Name"
                    for ir in model.indexRefs do
                        comp<MudSelectItem<IndexRef>>{
                            "Value" => ir
                            match ir with Azure n -> n.Name
                        }                                    
                }           
            }
            comp<MudPaper> {
                "Class" => "d-flex flex-none"
                comp<MudIconButton> {
                    "Icon" => Icons.Material.Outlined.Refresh
                    on.click(fun _ -> dispatch (RefreshIndexes false))
                }                
            }
        }

