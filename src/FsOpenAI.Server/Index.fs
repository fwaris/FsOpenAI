module FsOpenAI.Server.Index
open Bolero
open Bolero.Html
open Bolero.Server.Html
open MudBlazor
open FsOpenAI

let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)
let page = doctypeHtml {
    head {
        meta { attr.charset "UTF-8" }
        meta { attr.name "viewport"; attr.content "width=device-width, initial-scale=1.0" }
        title { "FsOpenAI" }
        ``base`` { attr.href "/" }
        link {attr.rel "short icon"; attr.``type`` "image/png"; attr.href "favicon.png"}
        link { attr.rel "stylesheet"; attr.href "https://cdnjs.cloudflare.com/ajax/libs/bulma/0.7.4/css/bulma.min.css" }
        link { attr.rel "stylesheet"; attr.href "css/index.css" }
        //mud blazor
        link {attr.href "https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap"; attr.rel "stylesheet"}
        link {attr.href "_content/MudBlazor/MudBlazor.min.css"; attr.rel "stylesheet"}
        script {attr.src "_content/MudBlazor/MudBlazor.min.js"}
        script {attr.src "scripts/utils.js"}
    }
    body {
        //attr.style "background: #32333dff; height: 100vh; display:flex; overflow; hidden"
        //attr.style "background: #32333dff;"
        //div { attr.id "main"; attr.style "background: #32333dff;"; comp<Client.App.MyApp> }
        div { attr.id "main"; comp<Client.App.MyApp> }
        boleroScript
        //comp<MudContainer> {
        //    "Style" => "background: #32333dff;"
        //    "MaxWidth" => MaxWidth.ExtraExtraLarge
        //}
    }
}
