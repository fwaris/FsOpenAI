#load "ScriptEnv.fsx"
open FsOpenAI.Shared
open FsOpenAI.GenAI
open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization


let url = @"E:\s\rtapi\ClientEvents.txt"
open FSharp.Data
let d = HtmlDocument.Load(url)
let evts = 
    d.CssSelect(".section") 
    |> List.filter (fun n -> 
        n.CssSelect(".anchor-heading-link")
        |> List.tryHead 
        |> Option.bind(fun y -> y.TryGetAttribute("href"))
        //|> Option.map(fun z -> z.Value().Contains("realtime-client-event"))
        |> Option.map(fun z -> z.Value().Contains("realtime-server-event"))
        |> Option.defaultValue false        
        )
let codes = 
    evts
    |> Seq.collect(fun n -> n.CssSelect(".code-sample"))
    |> Seq.toList
    |> List.map(fun n -> n.InnerText())

//File.WriteAllLines(@"E:\s\rtapi\ClientEventsCodes.txt", codes)
File.WriteAllLines(@"E:\s\rtapi\ServerEventsCodes.txt", codes)

let d2 = d.Body().InnerText()

