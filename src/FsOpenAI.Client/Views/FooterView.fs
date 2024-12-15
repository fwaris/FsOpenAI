namespace FsOpenAI.Client.Views
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open System
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
  

module Footer = 
    open Microsoft.JSInterop
    let copyToClipboard (jsr:IJSRuntime) (text:string) =
        jsr.InvokeVoidAsync ("navigator.clipboard.writeText", text) |> ignore

    let view jsr model dispatch = 
        comp<RadzenFooter> {                
            //"Style" => "height:3rem;"
            comp<RadzenRow>  {
                comp<RadzenColumn> {
                    "Size" => 1
                }                
                comp<RadzenColumn> {
                    "Size" => 10
                    match model.appConfig.Disclaimer with
                    | Some t ->    
                        comp<RadzenText> {
                            "TextAlign" => TextAlign.Center
                            "Text" => t
                            "Style" => "color:var(--rz-text-color-secondary);"
                        }
                    | None -> ()
                }
                comp<RadzenColumn> {
                    "Size" => 1
                    comp<RadzenButton> {
                        "ButtonStyle" => ButtonStyle.Base
                        "Size" => ButtonSize.Small
                        "Variant" => Variant.Outlined
                        "Style" => "background:transparent;border: 1px solid var(--rz-base-light)"
                        attr.``class`` "rz-border-radius-10 rz-shadow-10"
                        attr.title "Copy chat to clipboard"
                        "Icon" => "content_copy"
                        attr.callback "Click" (fun (e:MouseEventArgs) -> 
                            Model.selectedChat model
                            |> Option.iter (fun c ->
                                let text = Interaction.getText c
                                copyToClipboard jsr text
                                dispatch (ShowInfo "Copied")))                            
                    }
                }
            }
        }
