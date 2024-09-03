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
    inherit ElmishComponent<Model,Message>()

    override this.View model dispatch =
        let chat = Model.selectedChat model
        let bag = chat |> Option.bind Interaction.docContent |> Option.defaultValue DocumentContent.Default
        let isChecked = chat |> Option.map (fun ch -> ch.Mode = M_Doc || ch.Mode = M_Doc_Index) |> Option.defaultValue false
        printfn "isChecked: %b" isChecked
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
                    "Text" => (IO.browserFile bag.DocumentRef |> Option.map (fun x -> x.Name) |> Option.defaultValue "No document selected")
                }
            }
            comp<RadzenIcon> {
                "Icon" => icon
                attr.title title
                "IconColor" => color
            }
        }


