#load "ScriptEnv.fsx"
open System
open System.Text
open System.IO
open FsOpenAI.Shared
open Utils

let path = homePath.Value @@ ".fsopenai/ServiceSettings.json"

let base64 = path |> File.ReadAllText |> Encoding.UTF8.GetBytes |> Convert.ToBase64String
printfn "%s" base64




 