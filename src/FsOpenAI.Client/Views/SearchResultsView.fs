namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components.Web
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open FsOpenAI.Shared
open System.Linq.Expressions
open Radzen.Blazor.Rendering

type SVAttr =
    static member Property(expr:Expression<Func<DocRef,string>>) =
        "Property" => expr

type SearchResultsView() =
    inherit ElmishComponent<string option*DocRef list,Message>()    
    let popup = Ref<Popup>()
    let button = Ref<RadzenButton>()

    override this.View model dispatch =
        let chatId,docs = model
        concat {
            comp<RadzenButton> {
                "Style" => "background:transparent;height:2rem;"
                "Icon" => "snippet_folder"
                "ButtonStyle" => ButtonStyle.Base
                attr.title "Preview search results"
                attr.callback "Click" (fun (e:MouseEventArgs) -> popup.Value |> Option.iter (fun p -> p.ToggleAsync(button.Value.Value.Element) |> ignore))
                button
            }
            comp<Popup> {
                "Style" => $"display:none;position:absolute;max-height:90vh;max-width:90vw;height:20rem;width:30rem;padding:5px;background:transparent;"
                "Lazy" => false
                "PreventDefault" => false
                popup
                comp<RadzenCard> {
                    "Style" => "height:100%;width:100%;overflow:none;background-color:var(--rz-panel-background-color);"
                    "Variant" => Variant.Filled
                    attr.``class`` "rz-shadow-5 rz-p-2 rz-border-radius-5 rz-border-danger-light"
                    comp<RadzenDataList<DocRef>> {
                        "Style" => "height:100%;width:100%;overflow:none;"
                        attr.``class`` "rz-mb-2"
                        "Data" => docs
                        "Item" => "Document"
                        "AllowPaging" => true
                        "AllowVirtualization" => false
                        "PageSize" => 1
                        "Density" => Density.Compact
                        attr.fragmentWith "Template" (fun (o:DocRef) ->                             
                                comp<RadzenStack> {
                                    "Style" => "width:100%; height:100%; overflow:none;"
                                    comp<RadzenLink> {
                                        "Style" => "height:2rem;"
                                        "Path" => o.Ref
                                        "Target" => "_blank"
                                        o.Title
                                    }
                                    comp<RadzenRow> {
                                        attr.style "max-height:10rem; overflow:auto; white-space: pre-line;"
                                        div {
                                            attr.style "white-space: pre-line;"
                                            text o.Text
                                        }
                                    }
                                })                    
                    }
                }
            }
        }
