#load "Env.fsx"
open FsOpenAI.Client
open System.Text.Json

//shows how to create a settings json in a type-safe way

let serOpts = Utils.serOptions()
let settings =

        {
            AZURE_OPENAI_ENDPOINTS = []
            AZURE_SEARCH_ENDPOINTS = []
            BING_ENDPOINT = Some {API_KEY = "bing key"; ENDPOINT="https://bing.com"}
            OPENAI_KEY = None
        }
let str = JsonSerializer.Serialize(settings,serOpts)
printfn $"{str}"




