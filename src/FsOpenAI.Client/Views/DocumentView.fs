namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components.Forms

type DocumentView() =
    inherit ElmishComponent<DocBag*Interaction*Model,Message>()
    override this.View m dispatch =
        let dbag,chat,model = m
        let inputId = $"{chat.Id}_input"
        let textColor = match dbag.Document.Status with 
                        | Ready -> Color.Tertiary
                        | No_Document -> Color.Default
                        | _           -> Color.Warning

        comp<MudExpansionPanels> {                    
            "Class" => "d-flex flex-grow-1"
            comp<MudExpansionPanel> {
                attr.fragment "TitleContent" (
                    comp<MudPaper> {
                        "Class" => "d-flex flex-grow-1"
                        comp<MudFileUpload<IBrowserFile>> {
                            attr.id inputId
                            attr.callback "OnFilesChanged" (fun (e:InputFileChangeEventArgs) -> 
                                let doc = {dbag.Document with DocumentRef = Some e.File}
                                let dbag = {dbag with Document = doc}
                                dispatch (Ia_File_BeingLoad (chat.Id,dbag)))
                                //dispatch (Ia_UpdateDocBag (chat.Id,{dbag with Document = doc}))
                                //dispatch (Ia_File_Load chat.Id))

                            attr.fragmentWith "ButtonTemplate" (fun (t:String) -> 
                                comp<MudButton> {
                                    "Class" => "flex-none self-align-center ma-2"
                                    "Disabled" => (Update.isAuthorized model |> not)
                                    "Variant" => Variant.Filled
                                    "Color" => Color.Primary
                                    attr.``for`` inputId                           
                                    "HtmlTag" => "label"                                                                
                                    text "Document"
                                }
                            )                                                        
                        }
                        comp<MudIcon> {
                            "Class" => "flex-none align-self-center ma-2"
                            "Icon" => match dbag.Document.Status with 
                                        | No_Document -> Icons.Material.Outlined.UploadFile
                                        | Uploading -> Icons.Material.Outlined.Upload
                                        | Extracting -> Icons.Material.Outlined.Download
                                        | GenSearch -> Icons.Material.Outlined.Search
                                        | Ready -> Icons.Material.Outlined.Check
                            "Color" => textColor
                        }
                        comp<MudText> {                            
                            "Class" => "d-flex self-align-center ma-2"
                            text                            
                                (match dbag.Document.DocumentRef with 
                                | Some f -> $" [{Path.GetFileName(f.Name)}]" 
                                | None -> "")
                        }
                        comp<MudText> {
                            "Class" => "d-flex self-align-center ma-2"
                            "Color" => textColor
                            match dbag.Document.DocumentText with 
                            | None  -> "..."
                            | Some t -> if t.Length > 30 then t.Substring(0,30) + "..." else t
                        }
                    }
                )
                comp<MudGrid> {
                    "Class" => "d-flex flex-grow-1"
                    comp<MudItem> {
                        "xs" => 4
                        comp<MudTooltip> {                            
                            "Delay" => 200.
                            "Text" => "When checked, the entire document text is used for searching instead of the extracted search terms"
                            "Placement" =>  Placement.Right
                            "Arrow" => true
                            comp<MudSwitch<bool>> {
                                "Class" => "flex-shrink-1 ma-2"  
                                //"Label" => "Use entire document text"
                                "Checked" => dbag.SearchWithOrigText
                                attr.callback "CheckedChanged" (fun (e:bool) -> 
                                    dispatch (Ia_UpdateDocBag (chat.Id, {dbag with SearchWithOrigText = e})))
                                "Color" => if dbag.SearchWithOrigText then Color.Tertiary else Color.Default
                                text "Use original document text for search"
                            }       
                        }                    
                    }
                    comp<MudItem> {
                        "xs" => 8
                        comp<MudTextField<string>> {
                            "Class" => "flex-1 ma-2"
                            attr.callback "ValueChanged" (fun e -> dispatch (Ia_SetSearch (chat.Id,e)))
                            "Lines" => 4                                 
                            "Placeholder" => "search query to be extracted from document"
                            "Variant" => Variant.Outlined
                            "Label" => "Search Terms"
                            "Text" => match dbag.SearchTerms with Some q -> q | None -> null
                        }
                    }
                }
            }
        }