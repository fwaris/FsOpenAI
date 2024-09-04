namespace FsOpenAI.Client.Views
open System
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open Microsoft.AspNetCore.Components.Web
open Microsoft.JSInterop

module FooterBar =
    let copyToClipboard (jsr:IJSRuntime) (text:string) =
        jsr.InvokeVoidAsync ("navigator.clipboard.writeText", text) |> ignore

    let footer jsr model dispatch = 
        let chId,docs = TmpState.isDocsOpen model
        let bg = if model.darkTheme then model.theme.PaletteDark.Background else model.theme.PaletteLight.Background
        comp<MudAppBar> {
            "Style" => $"top: auto; bottom: 0; background:{bg.ToString(Utilities.MudColorOutputFormats.HexA)}" 
            "Dense" => true
            "Fixed" => true
            if model.interactions.Length > 0 then
                match model.appConfig.Disclaimer with 
                | Some t ->    
                    comp<MudPaper> {
                        "Elevation" => 0
                        "Style" => "width: 100vw;; max-height:3rem"
                        "Class" => "d-flex justify-center"                        
                        comp<MudText> {
                            "Type" => Typo.body2                            
                            "Color" => Color.Info
                            text t
                        }
                    }
                | None -> ()
            comp<MudFab> {
                "StartIcon" => Icons.Material.Outlined.ContentCopy
                //"EndIcon" => Icons.Material.Outlined.ContentCopy
                "Title" => "Copy chat to clipboard"
                "Size" => Size.Small
                "Style" => "position: absolute; bottom: 5px; right: 5px;"
                on.click(fun e -> 
                    Model.selectedChat model 
                    |> Option.iter(fun c -> 
                        let txt = Interaction.getText c
                        copyToClipboard jsr txt
                        dispatch (ShowInfo "Copied")))                            
            }
            ecomp<SearchResultsView,_,_> (chId,docs) dispatch {attr.empty()}
        }
