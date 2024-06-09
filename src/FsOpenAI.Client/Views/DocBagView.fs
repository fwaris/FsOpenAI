namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open Microsoft.AspNetCore.Components.Forms

type DocBagPopupView() =
    inherit ElmishComponent<DocBag*Interaction*Model,Message>()
    override this.View m dispatch =
        let dbag,chat,model = m
        let inputId = $"{chat.Id}_input"
        let textColor = match dbag.Document.Status with 
                        | Ready -> Color.Tertiary
                        | No_Document -> Color.Default
                        | _           -> Color.Warning
        comp<MudPopover> {
            "Open" => TmpState.isDocDetailsOpen chat.Id model
            "Style" => "width:50%;"
            "AnchorOrigin" => Origin.TopRight
            "TransformOrigin" => Origin.TopRight
            "Paper" => true
            "Class" => "d-flex flex-row flex-grow-1 align-start"
            comp<MudForm> {
                "Class" => "d-flex flex-grow-1 ma-3 flex-column"
                comp<MudTextField<string>> {
                    "Class" => "d-flex flex-grow-1 ma-2"
                    "Label" => "Document Name"
                    "Variant"  => Variant.Filled
                    "Placeholder" => "[no document selected]"
                    "Text" => (IO.browserFile dbag.Document.DocumentRef 
                                |> Option.map (fun f -> Path.GetFileName(f.Name)) 
                                |> Option.defaultValue null)
                    "ReadOnly" => true
                }                
                comp<MudTextField<string>> {
                    "Class" => "d-flex flex-grow-1 ma-2"
                    "Style" => $"color: {Colors.Green.Darken2};"
                    "Variant"  => Variant.Filled
                    attr.callback "ValueChanged" (fun e -> dispatch (Ia_SetSearch (chat.Id,e)))
                    "Lines" => 4                                 
                    "Placeholder" => "shows search terms extracted from document"
                    "Label" => "Search Terms"
                    "Disabled" => dbag.SearchWithOrigText
                    "Text" => match dbag.SearchTerms with Some q -> q | None -> null
                }
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
                        text "Search with full document text"
                    }       
                }                    
            }
            comp<MudPaper> {
                "Elevation" => 0
                comp<MudIconButton> {
                    "Icon" => Icons.Material.Outlined.ExpandLess
                    on.click (fun _ -> dispatch (Ia_ToggleDocDetails chat.Id))
                }
            }
        }                

type DocBagView() =
    inherit ElmishComponent<DocBag*Interaction*Model,Message>()
    override this.View m dispatch =
        let dbag,chat,model = m
        let inputId = $"{chat.Id}_input"
        let textColor = match dbag.Document.Status with 
                        | Ready -> Color.Tertiary
                        | No_Document -> Color.Default
                        | _           -> Color.Warning

        comp<MudPaper> {
            "Class" => "d-flex flex-grow-1 flex-row"
            comp<MudFileUpload<IBrowserFile>> {
                "Class" => "d-flex flex-grow-0 align-self-center ma-2"
                attr.id inputId
                attr.callback "OnFilesChanged" (fun (e:InputFileChangeEventArgs) -> 
                    let doc = 
                        {
                            DocumentRef = Some e.File
                            DocType = IO.docType e.File.Name
                            DocumentText = None
                            Status = DocumentStatus.No_Document                            
                        }
                    let dbag = {dbag with Document = doc; SearchTerms=None}
                    dispatch (Ia_File_BeingLoad (chat.Id,dbag)))

                attr.fragmentWith "ButtonTemplate" (fun (t:FileUploadButtonTemplateContext<IBrowserFile>) -> 
                    comp<MudButton> {
                        "Disabled" => (Auth.isAuthorized model |> not)
                        "Variant" => Variant.Filled
                        "Color" => Color.Primary
                        attr.``for`` inputId                           
                        "HtmlTag" => "label"                                                                
                        text "Document"
                    }
                )                                                        
            }
            comp<MudPaper> {
                "Class" => "d-flex align-self-center ma-3"
                "Elevation" => 0
                comp<MudIcon> {
                    "Title" => match dbag.Document.Status with 
                                | No_Document -> "No document selected"
                                | Uploading -> "Uploading document..."
                                | Receiving -> "Receiving document content ..."
                                | ExtractingTerms -> "Extracting search terms..."
                                | Ready -> "Document ready"
                    "Icon" => match dbag.Document.Status with 
                                | No_Document -> Icons.Material.Outlined.UploadFile
                                | Uploading -> Icons.Material.Outlined.Upload
                                | Receiving -> Icons.Material.Outlined.Download
                                | ExtractingTerms -> Icons.Material.Outlined.ContentPasteSearch
                                | Ready -> Icons.Material.Outlined.Check
                    "Color" => textColor
                }
            }
            comp<MudBreakpointProvider> {
                comp<MudHidden> {
                    "Breakpoint" => Breakpoint.SmAndDown
                    comp<MudText> {                            
                        "Class" => "ma-2 overflow-hidden"
                        "Style" => "max-height:3rem; max-width:10rem "
                        "Align" => Align.Center
                        text                            
                            (IO.browserFile dbag.Document.DocumentRef
                            |> Option.map (fun f -> $" [{Path.GetFileName(f.Name)}]") 
                            |> Option.defaultValue "")
                    }
                }
                comp<MudHidden> {
                    "Breakpoint" => Breakpoint.SmAndDown
                    comp<MudText> {
                        "Class" => "ma-2 overflow-hidden"
                        "Style" => "max-height:3rem; max-width:10rem"
                        "Color" => textColor
                        "Align" => Align.Center
                        match dbag.Document.DocumentText with 
                        | None  -> "..."
                        | Some t -> Utils.shorten 30 t                      
                    }
                }
            }
            comp<MudSpacer> {attr.empty()}
            comp<MudPaper> {
                "Class" => "d-flex align-self-center ma-1"
                "Elevation" => 0
                "Title" => "Details..."
                comp<MudIconButton> {
                    "Icon" => Icons.Material.Outlined.ExpandMore
                    on.click (fun _ -> dispatch (Ia_ToggleDocDetails chat.Id))
                }
            }
            ecomp<DocBagPopupView,_,_> (dbag,chat,model) dispatch {attr.empty()}
        }

