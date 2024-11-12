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
        //utils
        script {attr.src "scripts/utils.js"}
        //authentication
        script {attr.src "_content/Microsoft.Authentication.WebAssembly.Msal/AuthenticationService.js" }
        link { attr.rel "stylesheet"; attr.href "css/theme-override.css" }

    }
    body {
        div { 
                attr.id "main"
                comp<Client.App.MyApp> 
            }
        boleroScript
    }

    //radzen - note this needs to be after the body otherwise javascript does not find DOM elements
    //update version to force reload over older scripts that may be cached
    script {attr.src "_content/Radzen.Blazor/Radzen.Blazor.js?v=5.5.5"} 

}
