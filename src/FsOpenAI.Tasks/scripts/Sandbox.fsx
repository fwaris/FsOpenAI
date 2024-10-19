#load "ScriptEnv.fsx"
open FsOpenAI.Shared
open FsOpenAI.GenAI
open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

let url = @"E:\s\rtapi\RTAPI.txt"
open FSharp.Data
let d = HtmlDocument.Load(url)
let clientEvents = 
    d.CssSelect(".section") 
    |> List.filter (fun n -> 
        n.CssSelect(".anchor-heading-link")
        |> List.tryHead 
        |> Option.bind(fun y -> y.TryGetAttribute("href"))
        |> Option.map(fun z -> z.Value().Contains("realtime-client-event"))
        |> Option.defaultValue false        
        )

let serverEvents = 
    d.CssSelect(".section") 
    |> List.filter (fun n -> 
        n.CssSelect(".anchor-heading-link")
        |> List.tryHead 
        |> Option.bind(fun y -> y.TryGetAttribute("href"))
        |> Option.map(fun z -> z.Value().Contains("realtime-server-event"))
        |> Option.defaultValue false        
        )
        
let codes = 
    clientEvents @ serverEvents
    |> Seq.collect(fun n -> n.CssSelect(".code-sample"))
    |> Seq.toList
    |> List.map(fun n -> n.InnerText())

File.WriteAllLines(@"E:\s\rtapi\RTAPICodes.txt", codes)



