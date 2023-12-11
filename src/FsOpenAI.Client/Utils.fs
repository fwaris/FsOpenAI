namespace FsOpenAI.Client
open System
open FSharp.Control
open Microsoft.Extensions.Logging
open System.Text.Json
open System.Text.Json.Serialization

module Utils =
    //let mutable private id = 0
    //let nextId() = Threading.Interlocked.Increment(&id)

    let newId() = 
        Guid.NewGuid().ToByteArray() 
        |> Convert.ToBase64String 
        |> Seq.takeWhile (fun c -> c <> '=') 
        |> Seq.toArray 
        |> String

    let notEmpty (s:string) = String.IsNullOrWhiteSpace s |> not
    let isEmpty (s:string) = String.IsNullOrWhiteSpace s 

    let isOpen key map = map |> Map.tryFind key |> Option.defaultValue false

    exception NoOpenAIKey of string

    let serOptions() = 
        let o = JsonSerializerOptions(JsonSerializerDefaults.General)
        o.WriteIndented <- true
        JsonFSharpOptions.Default()
            .WithAllowNullFields(true)
            .WithAllowOverride(true)          
            .AddToJsonSerializerOptions(o)                
        o
