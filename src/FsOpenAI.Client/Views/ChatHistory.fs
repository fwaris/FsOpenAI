namespace FsOpenAI.Client.Views
open System
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
open FsOpenAI.Client

module Chats = 
    let userMessage model dispatch = 
        let bg = "background-color: transparent;"
        comp<RadzenRow> {                                        
            "Style" => bg
            attr.``class`` "rz-mt-1"
            comp<RadzenColumn> {
                "Size" => 12
                attr.style "display: flex; justify-content: flex-end;"
                comp<RadzenStack> {
                    "Orientation" => Orientation.Horizontal
                    attr.``class`` $"rz-border-radius-5 rz-p-8; rz-background-color-info-lighter"
                    div {
                        attr.style "white-space: pre-line;"
                        attr.``class`` "rz-p-2"
                        text "this is a user message\nwith mutliple lines"                        
                    }
                    comp<RadzenMenu> {
                        "Style" => bg
                        "Responsive" => false
                        comp<RadzenMenuItem> {
                            "Icon" => "refresh"
                            attr.title "Edit and resubmit this message"
                            attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch ToggleSideBar)
                        }
                    }                }
            }

        }

    let tm = """<svg version="1.1" viewBox="0 0 76.728 91.282" xmlns="http://www.w3.org/2000/svg">
 <g transform="matrix(.2857 0 0 .2857 71.408 28.262)" fill="#e20074">
  <path d="m-33.599 218.73v-22.192h-15.256c-26.315 0-38.393-15.643-38.393-38.665v-232.6h4.5246c49.283 0 80.582 32.707 80.582 80.797v4.3092h18.745v-107.3h-264.58v107.3h18.745v-4.3092c0-48.09 31.298-80.797 80.582-80.797h4.5246v232.6c0 23.022-12.078 38.665-38.393 38.665h-15.256v22.192z"/>
  <path d="m16.603 111.43h-62.914v-63.129h62.914z"/>
  <path d="m-185.07 111.43h-62.914v-63.129h62.914z"/>
 </g>
</svg>"""
    
    let systemMessage x lastMsg model dispatch =         
        let icon = "assistant"
        let background = "rz-border-danger-dark" 
        let icnstyl = IconStyle.Warning
        comp<RadzenCard> {
            attr.``class`` $"rz-mt-1 rz-border-radius-3"
            comp<RadzenRow> {
                comp<RadzenColumn> {
                    "Size" => 1
                    comp<RadzenIcon> {
                        "Icon" => tm
                        "IconStyle" => icnstyl
                        on.click (fun _ -> dispatch ToggleSideBar)
                    }
                }
                comp<RadzenColumn> {
                    "Size" => 10
                    div {
                        attr.style "white-space: pre-line;"
                        text $"{x} This is a message \n with multiple \n lines \n more \n more line \n"
                    }
                }
                comp<RadzenColumn> {
                    "Size" => 1
                    "Style" => "display:flex; flex-direction: column; justify-content: space-between;"
                    comp<RadzenMenu> {
                        "Responsive" => false
                        comp<RadzenMenuItem> {
                            "Disabled" => true
                            "Icon" => ""
                            attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch ToggleSideBar)
                        }
                    }
                    if lastMsg then
                        comp<RadzenMenu> {
                            "Responsive" => false
                            comp<RadzenMenuItem> {
                                "Icon" => "thumbs_up_down"
                                attr.title "Feedback"
                                attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch ToggleSideBar)
                            }
                        }
                }
            }
        }

    let history model dispatch =
        comp<RadzenRow> {
            "Style" => "height: auto;"
            comp<RadzenColumn> {
                "Style" => "height: auto;"
                comp<RadzenRow> {
                    "Style" => "max-height: calc(100vh - 17rem);overflow:auto;"
                    comp<RadzenColumn> {
                        let data = List.indexed [for c in 'A' .. 'Z' -> c.ToString()]
                        let lastI = data.Length - 1
                        for (i,x) in data do
                            if i%2 = 0 then
                                userMessage model dispatch
                            else
                                systemMessage x (i = lastI) model dispatch
                    }
                }
                comp<RadzenRow> {
                    "Style" => "height: auto; margin-right: 1rem; margin-top: 1rem;" 
                    ecomp<QuestionView,_,_> model dispatch {attr.empty()}
                }
            }
        }
