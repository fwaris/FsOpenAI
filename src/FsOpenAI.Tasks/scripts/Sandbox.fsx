//need .net sdk 9 installed
#load "ScriptEnv.fsx"
open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FsOpenAI
open FsOpenAI.Shared
open FsOpenAI.Shared.Utils
let (@@) (a:string) (b:string) = Path.Combine(a,b)
let path = Utils.homePath.Value @@ ".fsopenai" @@ "ServiceSettings.json"
let txt = File.ReadAllText path
let settings = JsonSerializer.Deserialize<ServiceSettings>(txt,Utils.serOptions())




