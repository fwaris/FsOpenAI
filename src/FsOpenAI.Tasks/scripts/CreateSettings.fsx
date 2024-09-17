#load "ScriptEnv.fsx"
open FsOpenAI.Shared
open System
open System.IO
open System.Text.Json

//shows how to create a settings json in a type-safe way

let serOpts = Utils.serOptions()
let settings =
        {
            LOG_CONN_STR = None // Some "cosmosdb connection string"
            AZURE_OPENAI_ENDPOINTS = [{API_KEY ="api key"; RESOURCE_GROUP="rg"}]
            AZURE_SEARCH_ENDPOINTS = []
            EMBEDDING_ENDPOINTS = []
            BING_ENDPOINT = None // Some {API_KEY = "bing key"; ENDPOINT="https://bing.com"}
            OPENAI_KEY = None
            GOOGLE_KEY = None
        }
let str = JsonSerializer.Serialize(settings,serOpts)
printfn $"{str}"

let str2 = File.ReadAllText (Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\.fsopenai/fsopenai1server/ServiceSettings.json"))
let str3 = str2 |> Text.Encoding.UTF8.GetBytes |> Convert.ToBase64String
printfn $"{str3}"
let settings' = JsonSerializer.Deserialize<ServiceSettings>(str2, serOpts)


