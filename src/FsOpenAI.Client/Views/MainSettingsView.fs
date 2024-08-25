namespace FsOpenAI.Client.Views
open System
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open Elmish
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open FsOpenAI.Shared
open System.Collections.Generic

type OpenAIKey() =
    inherit ElmishComponent<Model,Message>()

    override this.View model (dispatch:Message -> unit) =
        comp<RadzenCard> {
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "AlignItems" => AlignItems.Center
                comp<RadzenLabel> {"Text" => "OpenAI Key"}
                comp<RadzenTextBox> {
                    "Placeholder" => "OpenAI Key"
                    "Value" => (model.serviceParameters |> Option.bind(fun p -> p.OPENAI_KEY) |> Option.defaultValue null)
                    attr.callback "ValueChanged" (fun e -> dispatch (UpdateOpenKey e))
                }
            }
        }

type private M_MenuItems = 
    | M_ClearChats
    | M_PurgeLocalData
    | M_SetOpenAIKey

type MainSettingsView() =
    inherit ElmishComponent<Model,Message>()    

    [<Inject>]
    member val DialogService = Unchecked.defaultof<DialogService> with get, set

    [<Inject>]
    member val ContextMenuService = Unchecked.defaultof<ContextMenuService> with get, set
    
    override this.View model (dispatch:Message -> unit) =
        comp<RadzenButton> { 
            "Icon" => "menu"
            "Variant" => Variant.Flat
            "ButtonStyle" => ButtonStyle.Base
            "Size" => ButtonSize.Small
            attr.callback "Click" (fun (e:MouseEventArgs) -> 
                this.ContextMenuService.Open(
                    e,
                    [
                        ContextMenuItem(Icon="delete_sweep", Text="Clear Chats", Value=M_ClearChats, IconColor=Colors.Warning)
                        ContextMenuItem(Icon="folder_delete", Text="Purge all data stored in local browser storage", Value=M_PurgeLocalData)
                        if model.appConfig.EnabledBackends |> List.contains OpenAI then
                            ContextMenuItem(Icon="key", Text="Set OpenAI Key", Value=M_SetOpenAIKey)
                    ],
                    fun (e:MenuItemEventArgs) -> 
                        match e.Value :?> M_MenuItems with
                        | M_ClearChats -> dispatch Ia_ClearChats
                        | M_PurgeLocalData -> dispatch PurgeLocalData
                        | M_SetOpenAIKey -> 
                            let parms = ["Model", model :> obj; "Dispatch", dispatch] |> dict |> Dictionary
                            this.DialogService.OpenAsync<OpenAIKey>("OpenAI Key", parameters=parms) |> ignore
                        | _ -> ()
                ))
            }
        
        //         comp<RadzenAppearanceToggle> {attr.empty()} 
        //         comp<RadzenMenuItem> {
        //             "Icon" => "delete_sweep"
        //             attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch Ia_ClearChats)
        //             "Text" => "Clear Chats"
        //         }
        //         comp<RadzenMenuItem> {
        //             "Icon" => "folder_delete"
        //             "IconColor" => Colors.Warning
        //             attr.callback "Click" (fun (e:MenuItemEventArgs) -> dispatch PurgeLocalData)
        //             "Text" => "Purge all data stored in local browser storage"
        //         }
        //         if model.appConfig.EnabledBackends |> List.contains OpenAI then
        //             comp<RadzenMenuItem> {
        //                 "Icon" => "key"
        //                 attr.callback "Click" (fun (e:MenuItemEventArgs) -> 
        //                     let parms = ["Model", model :> obj; "Dispatch", dispatch] |> dict |> Dictionary
        //                     this.DialogService.OpenAsync<OpenAIKey>("OpenAI Key", parameters=parms) |> ignore)                
        //                 "Text" => "Set OpenAI Key"
        //             }
        //     }
        // }

(*
type MainSettingsView() =
    inherit ElmishComponent<Model,Message>()    
    override this.View mdl (dispatch:Message -> unit) =
        let settingsOpen = Utils.isOpen C.MAIN_SETTINGS mdl.settingsOpen
        comp<MudPopover> {
                "Style" => "margin-top:3rem; margin-right:5rem; width:75%; max-width:25rem;"
                "AnchorOrigin" => Origin.TopRight
                "TransformOrigin" => Origin.TopRight
                "Open" => settingsOpen
                "Paper" => true
                "Outlined" => true
                comp<MudStack> {
                    comp<MudStack> {
                        "Row" => true
                        comp<MudText> {
                            "Class" => "px-4"
                            "Typo" => Typo.h6
                            "Settings"
                        }
                        comp<MudSpacer>{
                            attr.empty()
                        }
                        comp<MudIconButton> {
                            "Class" => "align-self-end"
                            "Icon" => Icons.Material.Filled.Close
                            on.click (fun e -> dispatch (OpenCloseSettings C.MAIN_SETTINGS))
                        }
                    }
                    comp<MudTextField<string>> {
                        "Label" => "OpenAI Key"
                        "Class" => "ma-2"
                        "InputType" => InputType.Password
                        attr.callback "ValueChanged" (fun e -> dispatch (UpdateOpenKey e))
                        "Variant" => Variant.Outlined
                        "Placeholder" => "key stored locally"
                        "Text" => (mdl.serviceParameters |> Option.bind(fun p -> p.OPENAI_KEY) |> Option.defaultValue "")
                    }
                }
            }
        
*)
