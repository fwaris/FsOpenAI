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

type DocView() =
    inherit ElmishComponent<Model,Message>()

    [<Inject>]
    member val DialogService  = Unchecked.defaultof<DialogService> with get, set

    override this.View model dispatch =
        let chat = Model.selectedChat model
        let bag = chat |> Option.bind Interaction.docContent |> Option.defaultValue DocumentContent.Default
        let fileName = IO.browserFile bag.DocumentRef |> Option.map (fun x -> x.Name) |> Option.defaultValue "No document selected"
        let isChecked = chat |> Option.map (fun ch -> ch.Mode = M_Doc || ch.Mode = M_Doc_Index) |> Option.defaultValue false
        printfn "isChecked: %b %A" isChecked (chat |> Option.map (fun ch -> ch.Mode))
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
            comp<RadzenStack> {
                "Gap" => "0.5rem"
                "Orientation" => Orientation.Horizontal
                "AlignItems" => AlignItems.Center
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    "AlignItems" => AlignItems.Center
                    comp<RadzenCheckBox<bool>> {
                        "Value" => isChecked
                        attr.callback "ValueChanged" (fun (v:bool) -> chat|> Option.iter (fun ch -> dispatch (Ia_Mode_Document ch.Id)))
                    }
                    comp<RadzenLabel> {
                        "Text" => fileName 
                    }
                }
                comp<RadzenIcon> {
                    "Icon" => icon
                    attr.title title
                    "IconColor" => color
                }
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    comp<RadzenButton> {
                        "Style" => "background:transparent;"
                        "Icon" => "delete"
                        "ButtonStyle" => ButtonStyle.Base
                        "Size" => ButtonSize.ExtraSmall
                        attr.callback "Click" (fun (e:MouseEventArgs) -> chat|> Option.iter (fun ch -> dispatch (Ia_Remove_Document ch.Id)))
                    }                    
                    comp<RadzenButton> {
                        "Style" => "background:transparent;height:2rem;"
                        "Icon" => "more_horiz"
                        attr.title "Document details"
                        "ButtonStyle" => ButtonStyle.Base
                        attr.callback "Click" (fun (e:MouseEventArgs) ->
                            let parms = ["Model",model :> obj; "Dispatch",dispatch] |> dict |> Dictionary
                            let opts = DialogOptions(Width = "50%", Height="50%")
                            this.DialogService.OpenAsync<DocDetailsDialog>(fileName, parameters=parms, options=opts) |> ignore
                        )
                    }                

                }
            }
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
        }
