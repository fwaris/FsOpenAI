//need .net sdk 9 installed
#load "ScriptEnv.fsx"
open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FsOpenAI
open FsOpenAI.GenAI

let fn = @"C:\s\gc\Untitled-1.json" |> File.ReadAllText
let serOpts = Sessions.sessionOptions.Value

let fno = JsonSerializer.Deserialize<ChatSession>(fn, serOpts)



