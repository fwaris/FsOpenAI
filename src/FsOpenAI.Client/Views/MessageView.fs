namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open Microsoft.AspNetCore.Components.Web
open Microsoft.AspNetCore.Components

type MessageView() =
    inherit ElmishComponent<bool*Interaction*InteractionMessage*Model,Message>()

    let iconType (c:InteractionMessage)  = if c.IsUser then Icons.Material.Filled.Person else Icons.Material.Filled.Assistant

    let icon (c:InteractionMessage) =
        comp<MudIcon> {
            "Style" => "padding-right:10px;"
            "Icon" => iconType c
            "Size" => Size.Medium
        }

    let padding (c:InteractionMessage) = if c.IsUser then "margin-right:20px;" else "margin-left:20px"

    let border (c:InteractionMessage) = if c.IsUser then "mud-border-primary" else "mud-border-warning"

    override this.View model dispatch =
        let isBusy,chat,msg,model = model
        let docs = match msg.Role with Assistant r -> r.Docs | _ -> []
        let backColor = if model.darkTheme then Colors.Gray.Darken4 else Colors.Amber.Lighten4

        comp<MudPaper> {
            "Class" => $"d-flex border-solid border flex-column {border msg} rounded-lg pa-1"
            "Style" => $"{padding msg}; background-color:{backColor};"
            //"Style" => $"{padding msg} border-solid border-5 mud-border-primary"
            "Elevation" => 0
            comp<MudPaper> {
                "Class" => "d-flex flex-row "
                "Style" => $"background-color:{backColor};"
                "Elevation" => 0
                comp<MudPaper> {
                    "Class" => "d-flex flex-grow-1 ma-1 overflow-auto"  
                    "Style" => $"background-color:{backColor};"
                    "Elevation" => 0
                    concat {
                        icon msg
                        div {
                            attr.style "white-space: pre-line;"
                            Model.blockQuotes msg.Message
                        }
                    }
                }
                comp<MudPaper> {
                    "Class" => "d-flex flex-none align-start ma-1"
                    "Elevation" => 0
                    "Style" => $"background-color:{backColor};"
                    if msg.IsUser then 
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Outlined.RestartAlt
                            "Size" => Size.Small
                            "Title" => "Restart chat from here"
                            "Disabled" => chat.IsBuffering 
                            on.click(fun e -> dispatch (Ia_Restart (chat.Id,msg)))
                        }   
                }
            }
            if not docs.IsEmpty && not chat.IsBuffering then       
                table {                              
                    tr {
                        td {    
                            attr.style "width: 1.5rem;"               
                            comp<MudIconButton> {
                                "Class" => "align-self-center"
                                "Title" => "Show search results"
                                "Icon" => Icons.Material.Outlined.SnippetFolder
                                on.click (fun _ -> dispatch (Ia_ToggleDocs (chat.Id, Some msg.MsgId)))
                            }
                        }
                        td {
                            comp<MudPaper> {
                                "Class" => "pa-2 overflow-auto"
                                "Style" => "height: 3.5rem;"
                                for d in docs do
                                    comp<MudTooltip> {
                                        "Text" => Utils.shorten 40 d.Text                                    
                                        comp<MudLink> { 
                                            "Class" => "ml-2 align-self-center"
                                            "Style" => "max-width: 140px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;"
                                            "Href" => d.Ref
                                            "Target" => "_blank"
                                            d.Title
                                        }                                    
                                    }                    
                            }
                        }
                    }
                }
        }
