#load "ScriptEnv.fsx"
open FsOpenAI.Shared
open System
open System.IO
open System.Text.Json
open FsOpenAI.Shared.Utils
open System.Text.Json.Serialization

let strA = """
{
  "AZURE_SEARCH_ENDPOINTS": [],
  "CHAT_ENDPOINTS": [

    {
      "API_KEY": "cxs ... xxx",
      "ENDPOINT": "https://prde....",
      "BACKEND" : "AzureOpenAI"
      
    }
]
  // "BING_ENDPOINT": null,
  //"OPENAI_KEY": null,
  //"LOG_CONN_STR": null
}
"""

let jstrA = JsonSerializer.Deserialize<ServiceSettings>(strA,Utils.settingSerOpts())

let settings =
        {
            LOG_CONN_STR = None // Some "cosmosdb connection string"
            CHAT_ENDPOINTS = [{API_KEY ="api key"; ENDPOINT="http://api.openai.com"; BACKEND = "AzureOpenAI"}]
            AZURE_SEARCH_ENDPOINTS = []
            BING_ENDPOINT = None // Some {API_KEY = "bing key"; ENDPOINT="https://bing.com"}
            OPENAI_KEY = None
           // GOOGLE_KEY = None
        }
let str = JsonSerializer.Serialize(settings,Utils.settingSerOpts())
printfn $"{str}"
let defPath = Utils.homePath.Value @@ ".fsopenai" @@ "poc" @@ "ServiceSettings.json"
let pocStr = File.ReadAllText defPath
printfn "%s" pocStr
let s2 = JsonSerializer.Deserialize<ServiceSettings>(File.ReadAllText defPath,Utils.settingSerOpts())


let dir = Path.GetDirectoryName defPath
if not (Directory.Exists dir) then Directory.CreateDirectory dir |> ignore
File.WriteAllText(defPath, str)
