module FsOpenAI.Server.Index
open System
open System.Text
open Bolero
open Bolero.Html
open Bolero.Server.Html
open FsOpenAI
open FsOpenAI.Shared

let page = doctypeHtml {
    let scriptVersion = "3"
    let uiVersion = typeof<Radzen.RadzenComponent>.Assembly.GetName().Version.ToString();

    let appConfig = FsOpenAI.GenAI.Env.appConfig.Value
    let tabTitle = appConfig |> Option.bind _.AppName |> Option.defaultValue ""
    let appTitle = 
        appConfig 
        |> Option.map _.AppBarType
        |> Option.bind (function 
            | Some (AppB_Base t) -> Some t
            | Some (AppB_Alt t) -> Some t
            | None -> None)
    let requireLogin = appConfig |> Option.map _.RequireLogin |> Option.defaultValue false
    let cfg = {AppTitle=appTitle; RequireLogin=requireLogin}
    let cfgStr = Json.JsonSerializer.Serialize(cfg, Utils.serOptions()) |> Encoding.UTF8.GetBytes |> Convert.ToBase64String

    head {
        meta { attr.charset "UTF-8" }
        meta { attr.name "viewport"; attr.content "width=device-width, initial-scale=1.0" }
        title { tabTitle }
        ``base`` { attr.href "/" }
        link {attr.rel "short icon"; attr.``type`` "image/png"; attr.href "app/imgs/favicon.png"}
        link { attr.rel "stylesheet"; attr.href $"css/index.css?v={scriptVersion}" }
        link { attr.rel "stylesheet"; attr.href $"css/hover.css?v={scriptVersion}" }
        //utils
        script {attr.src $"scripts/utils.js?v={scriptVersion}"}        
        //authentication
        script {attr.src "_content/Microsoft.Authentication.WebAssembly.Msal/AuthenticationService.js" }
        link { attr.rel "stylesheet"; attr.href "css/theme-override.css?v=2" }
    }
    body {
        input {attr.id C.LOAD_CONFIG_ID; attr.``type`` "hidden"; attr.value cfgStr; }

        div{ 
            attr.id "main"
            div {            
                attr.style "position:absolute; top:1vh; width:100%; text-align:center"
                div {
                    attr.``class`` "spinner-overlay"
                    div {
                        attr.style "display:flex; flex-direction: column; align-items: center;"
                        h3{
                            attr.style "display: inline-block;"; 
                            text $"""{appTitle |> Option.defaultValue "Application"} ..."""
                        }
                        div {
                            attr.``class`` "spinner" 
                            attr.style "display: inline-block;";
                            attr.aria "label" "Loading..."
                            "role" => "status"
                        }
                    }
                }
            }
            comp<Client.App.MyApp> 
        }
        boleroScript
    }

    //radzen - note this needs to be after the body otherwise javascript does not find DOM elements
    //update version to force reload over older scripts that may be cached
    script {attr.src $"_content/Radzen.Blazor/Radzen.Blazor.js?v={uiVersion}"} //version change forces js refresh on client
}
