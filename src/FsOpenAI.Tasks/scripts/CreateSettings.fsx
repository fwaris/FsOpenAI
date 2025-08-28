#load "ScriptEnv.fsx"
open FsOpenAI.Shared
open System
open System.IO
open System.Text.Json
open FsOpenAI.Shared.Utils


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
           // GOOGLE_KEY = None
        }
let str = JsonSerializer.Serialize(settings,serOpts)
printfn $"{str}"
let defPath = Utils.homePath.Value @@ ".fsopenai/ServiceSettings.json"
let dir = Path.GetDirectoryName defPath
if not (Directory.Exists dir) then Directory.CreateDirectory dir |> ignore
File.WriteAllText(defPath, str)
