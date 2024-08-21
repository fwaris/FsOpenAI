namespace FsOpenAI.Client.Views
open FsOpenAI
open System
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
  

module Comps = 

    let footer model dispatch = 
        comp<RadzenFooter> {                
            comp<RadzenRow>  {
                comp<RadzenColumn> {
                    "Size" => 12
                    comp<RadzenText> {
                        "Text" => "Footer"
                    }
                }
            }
        }
