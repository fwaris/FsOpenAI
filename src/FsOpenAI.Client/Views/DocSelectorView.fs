namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open Microsoft.AspNetCore.Components.Forms
open FsOpenAI.Shared

type DocTextPopupView() =
    inherit ElmishComponent<DocumentContent*Interaction*Model,Message>()
    override this.View m dispatch =
        let docCntnt,chat,model = m
        let inputId = $"{chat.Id}_input"
        let textColor = match docCntnt.Status with 
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
                    "Text" => (IO.browserFile docCntnt.DocumentRef 
                                |> Option.map (fun f -> Path.GetFileName(f.Name)) 
                                |> Option.defaultValue null)
                    "ReadOnly" => true
                }                
                comp<MudTextField<string>> {
                    "Class" => "d-flex flex-grow-1 ma-2"
                    "Style" => $"color: {Colors.Green.Darken2};"
                    "Variant"  => Variant.Filled
                    "Lines" => 10                                 
                    "Placeholder" => "shows extracted document text, if any"
                    "Label" => "Extracted Text"
                    "ReadOnly" => true
                    "Text" => (docCntnt.DocumentText |> Option.defaultValue null)
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


type DocSelectorView() =
    inherit ElmishComponent<DocumentContent*Interaction*Model,Message>()
    override this.View m dispatch =
        let docCntnt,chat,model = m
        let inputId = $"{chat.Id}_input"
        let textColor = match docCntnt.Status with 
                        | Ready       -> Color.Tertiary
                        | No_Document -> Color.Default
                        | _           -> Color.Warning

        comp<MudPaper> {
            "Class" => "d-flex flex-grow-1 flex-row ma-2"
            comp<MudFileUpload<IBrowserFile>> {
                "Class" => "d-flex flex-grow-0 align-self-center ma-2"
                attr.id inputId
                attr.callback "OnFilesChanged" (fun (e:InputFileChangeEventArgs) -> 
                    let doc = {
                                DocumentRef = Some e.File
                                DocType = IO.docType e.File.Name
                                DocumentText = None
                                Status = DocumentStatus.No_Document 
                              }
                    dispatch (Ia_File_BeingLoad2 (chat.Id,doc)))

                attr.fragment "ActivatorContent" (
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
                    "Title" => match docCntnt.Status with 
                                | No_Document -> "No document selected"
                                | Uploading -> "Uploading document..."
                                | Receiving -> "Receiving document content ..."
                                | ExtractingTerms -> "Extracting search terms..."
                                | Ready -> "Document ready"
                    "Icon" => match docCntnt.Status with 
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
                        "Style" => "max-height:3rem; max-width:20rem "
                        "Align" => Align.Center
                        text                            
                            (IO.browserFile docCntnt.DocumentRef 
                             |> Option.map (fun f -> Path.GetFileName(f.Name))
                             |> Option.defaultValue "")
                    }
                }
                comp<MudHidden> {
                    "Breakpoint" => Breakpoint.SmAndDown
                    comp<MudText> {
                        "Class" => "ma-2 overflow-hidden"
                        "Style" => "max-height:3rem; max-width:20rem"
                        "Color" => textColor
                        "Align" => Align.Center
                        match docCntnt.DocumentText with 
                        | None  -> "..."
                        | Some t -> Utils.shorten 30 t                      
                    }
                }
            }
            comp<MudSpacer> {attr.empty()}
            comp<MudPaper> {
                "Class" => "d-flex align-self-center ma-1"
                "Elevation" => 0
                "Title" => "Document text..."
                comp<MudIconButton> {
                    "Icon" => Icons.Material.Outlined.ExpandMore
                    on.click (fun _ -> dispatch (Ia_ToggleDocDetails chat.Id))
                }
            }
            ecomp<DocTextPopupView,_,_> (docCntnt,chat,model) dispatch {attr.empty()}
        }

