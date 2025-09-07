namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open Radzen
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Radzen.Blazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open System.Collections.Generic
open FsOpenAI.Shared
open Microsoft.AspNetCore.Components.Forms

////need local binding for Radzen Popup as dispatch (page rendering) closes the popup
[<CLIMutable>]
type DocLoadModel = {
    ChatId : string
    Model : Model
    mutable file : IBrowserFile option
    mutable useOcr : bool
}

type DocAttachDialog() =
    inherit ElmishComponent<DocLoadModel,Message>()
    let check = Ref<RadzenSwitch>()
    let mutable change = false

    [<Inject>] member val DialogService  = Unchecked.defaultof<DialogService> with get, set

    override this.ShouldRender (): bool = 
        let ret = change 
        change <- false
        ret

    member this.TriggerChange() = 
        printfn "triggered"
        change <- true
        base.StateHasChanged()        

    override this.View model dispatch =
        let chat = Model.selectedChat model.Model
        let docCntnt = chat |> Option.bind Interaction.docContent |> Option.defaultValue DocumentContent.Default        
        concat {
            style {"""
                #uploadWithDragAndDrop {
                    left: 0;
                    --rz-upload-button-bar-background-color: transparent;
                    --rz-upload-button-bar-padding: 0;
                }

                #uploadWithDragAndDrop .rz-fileupload-buttonbar .rz-fileupload-choose {
                    width: 100%;
                    text-align: center;
                    font-size: 16px;
                    padding: 50px 10px;
                }
            """}
            comp<RadzenStack> {
                attr.``class`` "rz-h-100 rz-justify-content-center"
                "Orientation" => Orientation.Horizontal
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    attr.``class`` "rz-h-100 rz-background-color-secondary-lighter rz-p-2 "
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Horizontal
                        attr.``class`` "rz-h-100"
                        
                        comp<RadzenLabel> {
                            "Text" => "Use image-to-text for PDF"
                        }
                        comp<RadzenSwitch> {
                            attr.title "For PDFs, extract text via OCR processing of page images"
                            "Value" => model.useOcr
                            check
                        }                    
                    }
                    comp<RadzenUpload> {
                        attr.id "uploadWithDragAndDrop"
                        "ChooseText" => " Drag and drop a document here or click here to select a document "
                        attr.callback "Change" (fun (e:Radzen.UploadChangeEventArgs) ->
                            let browserFile = 
                                e.Files
                                |> Seq.tryHead 
                                |> Option.map (fun x -> x.Source)
                            this.Model.file <- browserFile
                            this.TriggerChange()
                        )
                    }
                    comp<RadzenButton> {
                        attr.disabled this.Model.file.IsNone
                        attr.callback "Click" (fun (e:MouseEventArgs) ->                      
                            let file = this.Model.file |> Option.map box
                            let docCntnt = 
                                {docCntnt with 
                                    DocumentRef = file; 
                                    DocTitle = this.Model.file |> Option.map _.Name
                                    ProcessingInfo=[]; 
                                    UsedOcr = check.Value.Value.Value
                                }                                
                            this.Dispatch (Ia_File_BeingLoad (this.Model.ChatId,docCntnt))
                            async {
                                do! Async.Sleep 200
                                this.DialogService.Close()
                            }
                            |> Async.Start)                        
                        text "Attach"
                    }
                }
            }            
        }


type DocDetailsDialog() =
    inherit ElmishComponent<Model,Message>()
    override this.View model dispatch =
        let chat = Model.selectedChat model
        let docCntnt = chat |> Option.bind Interaction.docContent |> Option.defaultValue DocumentContent.Default
        comp<RadzenTabs> {
            attr.fragment "Tabs" (
                concat {
                    comp<RadzenTabsItem> {
                        "Text" => "Document Text"
                        comp<RadzenText> {
                            "Cols" => 10
                            "Rows" => 30
                            "Text" => (docCntnt.DocumentText |> Option.defaultValue null) 
                        }
                    }
                    comp<RadzenTabsItem> {
                        "Text" => "Processing Info"
                        comp<RadzenTimeline> {
                            attr.``class`` "rz-mt-1 rz-p-2"
                            "LinePosition" => LinePosition.Left
                            attr.fragment "Items" (
                                concat {
                                    for t in docCntnt.ProcessingInfo do
                                        yield   
                                            concat{
                                                comp<RadzenBadge> {
                                                    "Shade" => Shade.Light
                                                    "BadgeStyle" => BadgeStyle.Base
                                                }
                                                comp<RadzenText> {
                                                    //"Style" => "max-width: 10rem; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;"
                                                    "TextStyle" => TextStyle.Caption
                                                    attr.title t
                                                    "Text" => t
                                                }
                                            }                                                
                                })
                        }                    
                    }
                    if Model.isEnabled M_Doc_Index model then
                        comp<RadzenTabsItem> {
                            "Text" => "Search Terms"
                            comp<RadzenText> {
                                "Cols" => 10
                                "Rows" => 30
                                "Text" => (docCntnt.SearchTerms |> Option.defaultValue null) 
                            }
                        }
                }
            )
        }

type DocViewCompact() = 
    inherit ElmishComponent<Model,Message>()

    [<Inject>]
    member val DialogService  = Unchecked.defaultof<DialogService> with get, set

    override this.View model dispatch =
        let chat = Model.selectedChat model
        let bag = chat |> Option.bind Interaction.docContent |> Option.defaultValue DocumentContent.Default
        let fileName = bag.DocTitle |> Option.defaultValue ""
        let isChecked = chat |> Option.map (fun ch -> ch.Mode = M_Doc || ch.Mode = M_Doc_Index) |> Option.defaultValue false
        let upld_status =
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
            "Gap" => "0.0rem"
            "Orientation" => Orientation.Horizontal
            "AlignItems" => AlignItems.Center
            attr.``class`` "hover-container"
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "AlignItems" => AlignItems.Center
                comp<RadzenLabel> {
                    "Style" => "max-width: 140px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 1; -webkit-box-orient: vertical; color: var(--rz-danger-light);"
                    "Text" => fileName 
                }
            }
            comp<RadzenIcon> {
                "Icon" => icon
                attr.title upld_status
                "IconColor" => color
            }
            comp<RadzenButton> {
                "Style" => "background:transparent;height:2rem;"
                "Icon" => "more_horiz"
                attr.title "Document details"
                "ButtonStyle" => ButtonStyle.Base
                attr.callback "Click" (fun (e:MouseEventArgs) ->
                    let parms = ["Model",model :> obj; "Dispatch",dispatch] |> dict |> Dictionary
                    let opts = DialogOptions(Width = "50%", Height="50%", Resizable=true, Draggable=true)
                    this.DialogService.OpenAsync<DocDetailsDialog>(fileName, parameters=parms, options=opts) |> ignore
                )
            }    
            div {
                attr.``class`` "hover-button"
                comp<RadzenButton> {
                    attr.title "Remove document"
                    "Style" => "background:transparent;"
                    "Icon" => "delete"
                    "ButtonStyle" => ButtonStyle.Base
                    "Size" => ButtonSize.ExtraSmall
                    attr.callback "Click" (fun (e:MouseEventArgs) -> chat|> Option.iter (fun ch -> dispatch (Ia_Remove_Document ch.Id)))
                }                    
            }            
        }
(*
            if Model.isEnabled M_Doc_Index model && isChecked then
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    "AlignItems" => AlignItems.Start
                    comp<RadzenStack> {
                        "Orientation" => Orientation.Horizontal
                        "AlignItems" => AlignItems.Center
                        attr.``class`` "rz-background-color-secondary-lighter rz-p-2"
                        comp<RadzenLabel> {
                            "Text" => "Combine with index"
                        }
                        comp<RadzenSwitch> {
                            "Value" => (chat |> Option.map (fun ch -> ch.Mode = M_Doc_Index) |> Option.defaultValue false)
                            attr.callback "Change" (fun (v:bool) -> chat |> Option.iter (fun chat -> dispatch (Ia_Mode_Doc_Index (chat.Id,v))))
                            attr.title "Use document content with selected indexes for question answering"
                        }
                    }
                }
*)


type DocAttachView() =
    inherit ElmishComponent<Model,Message>() 

    [<Inject>] member val DialogService  = Unchecked.defaultof<DialogService> with get, set

    override this.View (model: Model) (dispatch: Elmish.Dispatch<Message>): Node = 
        let selChat = Model.selectedChat model
        let isNotReady = not (Submission.isReady selChat)
        let doc = selChat |> Option.bind Interaction.docContent
        match doc with 
        | Some doc ->
            ecomp<DocViewCompact,_,_> model dispatch {attr.empty()}
        | None ->                     
            comp<RadzenMenu> {
                "Style" => "background-color: transparent;"
                "Responsive" => false                        
                comp<RadzenMenuItem> {
                    "Icon" => "attach_file"
                    attr.disabled  isNotReady
                    attr.title "Attach a document or image"
                    attr.callback "Click" (fun (e:MenuItemEventArgs) ->
                        let opts = new DialogOptions (                                    
                            Style = "width: fit-content; height: fit-content; min-width: fit-content; min-height: fit-content;"
                        )
                        let dmodel = {ChatId=selChat |> Option.map _.Id |> Option.defaultValue ""; Model=model; useOcr=model.appConfig.UseORCByDefault; file=None}
                        let parms = ["Model",dmodel :> obj; "Dispatch",dispatch] |> dict |> Dictionary
                        //let opts = DialogOptions(Width = "50%", Height="50%")
                        this.DialogService.OpenAsync<DocAttachDialog>("Document", parameters=parms, options=opts) |> ignore)
                }
            }
        