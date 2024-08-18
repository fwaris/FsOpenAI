namespace FsOpenAI.Client.Views
open FsOpenAI
open System
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open Radzen
open Radzen.Blazor
type Tree =
    {
        Text : string
        Children : Tree list
    }    

module Comps = 

    let indexTree  model dispatch = 
        comp<RadzenRow> {
            comp<RadzenColumn> {
                comp<RadzenCard> {                                    
                    comp<RadzenTree> {
                        "AllowCheckBoxes" => true
                        "Data" => 
                            [for i in 1..10 do
                                    yield 
                                        {
                                            Text = $"a {i}"; 
                                            Children = [ for j in 1 .. 3 -> {Text = $"b {j}"; Children=[] }] 
                            }]
                        comp<RadzenTreeLevel> {                                                
                            "TextProperty" => "Text"
                            "ChildrenProperty" => "Children"
                            "Expanded" => Func<_,_>(fun (t:obj) -> true) 
                            // attr.fragmentWith "Template" (fun (x:RadzenTreeItem) ->
                            //     comp<RadzenStack> {                                                    
                            //         "Orientation" => Orientation.Horizontal
                            //         comp<RadzenText> {
                            //             "Style" => "align-self: center;"
                            //             "Text" => x.Text
                            //         }
                            //         comp<RadzenBadge> {
                            //             attr.``class`` "rz-border-radius-6"
                            //             "Text" => x.Text
                            //         }
                            //     }
                            // )
                        }                                            
                    }
                }
            }
        }

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
