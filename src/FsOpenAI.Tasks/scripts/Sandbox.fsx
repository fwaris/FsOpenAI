#load "ScriptEnv.fsx"
open FsOpenAI.Shared
open FsOpenAI.GenAI
open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

let jsonFile = @"E:\s\genai\session_example.json"
let text = File.ReadAllText jsonFile
printfn "%s" text

let so() = 
    let o = JsonSerializerOptions(JsonSerializerDefaults.General)
    o.WriteIndented <- true
    o.ReadCommentHandling <- JsonCommentHandling.Skip
    let o' = JsonFSharpOptions.Default()
    o'
        //.WithUnwrapOption(true)
        //.WithAllowNullFields(true)
        //.WithAllowOverride(true)
        //.WithSkippableOptionFields(false)
        .WithUnionEncoding(JsonUnionEncoding.NewtonsoftLike)
        .AddToJsonSerializerOptions(o)        
    o

let sess= JsonSerializer.Deserialize<ChatSession>(text,so())
