namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
()

(*
type ChatCreateView() =
    inherit ElmishComponent<Model,Message>()    
    override this.View model (dispatch:Message -> unit) =
        let isOpen = (Utils.isOpen C.ADD_CHAT_MENU model.settingsOpen)
        comp<MudPopover> {
                "Style" => "margin-top:3rem; margin-right:5rem; width:20rem;"                
                "AnchorOrigin" => Origin.TopRight
                "TransformOrigin" => Origin.TopRight
                "Paper" => true
                "Open" => isOpen        
                Init.createMenu model dispatch
        }

*)