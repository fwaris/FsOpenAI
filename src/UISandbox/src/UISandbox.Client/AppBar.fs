namespace UISandbox.Client
open System
open Bolero.Html
open MudBlazor
open Microsoft.AspNetCore.Components.Web

module AppBar =

    let appBar model dispatch = 
        comp<MudAppBar> {
            "Fixed" => true
            "Dense" => true
            comp<MudGrid> {
                comp<MudItem> {
                    "xs" => 1
                    "sm" => 5
                    "Class" => "d-none d-sm-flex"
                    //"Class" => "d-flex justify-center align-content-center flex-grow-1"
                    comp<MudLink> {
                        "Href" => "#"
                        "Target" => "_blank"
                        comp<MudImage> {
                            "Class" => "mt-2"
                            "ObjectFit" => ObjectFit.ScaleDown
                            //"Height" => Nullable 40
                            "Width" => Nullable 160
                            "Src" => "imgs/logo.png"
                        }
                    }
                }
            }
        }    
