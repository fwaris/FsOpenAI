module FsOpenAI.Server.Index
open Bolero
open Bolero.Html
open Bolero.Server.Html
open FsOpenAI

let page = doctypeHtml {
    head {
        meta { attr.charset "UTF-8" }
        meta { attr.name "viewport"; attr.content "width=device-width, initial-scale=1.0" }
        title { "..." }
        ``base`` { attr.href "/" }
        link {attr.rel "short icon"; attr.``type`` "image/png"; attr.href "app/imgs/favicon.png"}
        link { attr.rel "stylesheet"; attr.href "css/index.css" }
        //radzen 
        script {attr.src "_content/Radzen.Blazor/Radzen.Blazor.js"}
        //utils
        script {attr.src "scripts/utils.js"}
        //authentication
        script {attr.src "_content/Microsoft.Authentication.WebAssembly.Msal/AuthenticationService.js" }
    }
    body {
        div { 
                attr.id "main"
                comp<Client.App.MyApp> 
            }
        boleroScript
    }
}
