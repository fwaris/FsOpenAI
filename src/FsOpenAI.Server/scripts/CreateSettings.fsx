#load "Env.fsx"
open FsOpenAI.Client
open System.Text.Json

//shows how to create a settings json in a type-safe way

let serOpts = Utils.serOptions()
let settings =

        {
            AZURE_OPENAI_ENDPOINTS = []
            AZURE_SEARCH_ENDPOINTS = []
            AZURE_OPENAI_MODELS = None
            BING_ENDPOINT = Some {API_KEY = "bing key"; ENDPOINT="https://bing.com"}
            OPENAI_MODELS = Some(
                {
                    CHAT = ["gpt-3.5-turbo-16k"; "gpt-3.5-turbo"; "gpt-4"]
                    COMPLETION = ["text-davinci-003"]
                    EMBEDDING = ["text-embedding-ada-002"]
                }
            )
            OPENAI_KEY = None
        }
let str = JsonSerializer.Serialize(settings,serOpts)
printfn $"{str}"




