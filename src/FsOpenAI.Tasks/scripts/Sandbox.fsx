//need .net sdk 9 installed
#load "ScriptEnv.fsx"
open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FsOpenAI
open FsOpenAI.Shared
open FsOpenAI.GenAI

let path = Path.GetTempPath()
let fn = Directory.GetFiles(path, $"*{C.UPLOAD_EXT}.*") 
fn |> Seq.iter (printfn "%s")




