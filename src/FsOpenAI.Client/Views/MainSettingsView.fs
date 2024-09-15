namespace FsOpenAI.Client.Views
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
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
            "Style"  => "background-color: transparent;"
            "Size" => ButtonSize.Small
            attr.callback "Click" (fun (e:MouseEventArgs) -> 
                this.ContextMenuService.Open(
                    e,
                    [
                        ContextMenuItem(Icon="delete_sweep", Text="Clear chats", Value=M_ClearChats, IconColor=Colors.Warning)
                        ContextMenuItem(Icon="folder_delete", Text="Purge local browser storage", Value=M_PurgeLocalData)
                        if model.appConfig.EnabledBackends |> List.contains OpenAI then
                            ContextMenuItem(Icon="key", Text="Set OpenAI Key", Value=M_SetOpenAIKey)
                    ],
                    fun (e:MenuItemEventArgs) -> 
                        match e.Value :?> M_MenuItems with
                        | M_ClearChats -> this.ContextMenuService.Close(); dispatch Ia_ClearChats
                        | M_PurgeLocalData -> this.ContextMenuService.Close(); dispatch PurgeLocalData
                        | M_SetOpenAIKey -> 
                            this.ContextMenuService.Close();
                            let parms = ["Model", model :> obj; "Dispatch", dispatch] |> dict |> Dictionary
                            this.DialogService.OpenAsync<OpenAIKey>("OpenAI Key", parameters=parms) |> ignore
                ))
            }
