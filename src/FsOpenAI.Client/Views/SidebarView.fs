namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared
open Radzen
open Radzen.Blazor
open FsOpenAI.Shared.Interactions
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web

type SidebarView() =
    inherit ElmishComponent<Model,Message>()

    override this.View model dispatch =        
        let selChatId = Model.selectedChat model |> Option.map (fun x -> x.Id) |> Option.defaultValue ""
        comp<RadzenSidebar> {
            "Expanded" => TmpState.isOpen C.SIDE_BAR_EXPANDED model
            attr.callback "ExpandedChanged" (fun (b:bool) -> dispatch ToggleSideBar)
            comp<RadzenDataList<Interaction>> {
                "Data" => model.interactions
                attr.fragmentWith "Template" (fun (x:Interaction) ->
                    comp<RadzenRow> {
                        comp<RadzenColumn> {
                            "Size" => 10
                            comp<RadzenButton> {
                                if x.Id = selChatId then                            
                                    "Style" => "outline: 1px solid var(--rz-primary); background-color: transparent; width: 100%;"
                                else 
                                    "Style" => "background-color: transparent; width: 100%;"
                                attr.callback "Click" (fun (e:MouseEventArgs) -> dispatch (Ia_Selected x.Id))
                                Interaction.label x
                            }                    
                        }
                        comp<RadzenColumn> {
                            "Size" => 1
                            comp<RadzenMenu> {
                                "Responsive" => false
                                comp<RadzenMenuItem> {
                                    "Icon" => "close"
                                    "Title" => "Delete chat"
                                    attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch (Ia_Remove x.Id))

                                }
                            }
                        }
                    }
                )
            }
        }
