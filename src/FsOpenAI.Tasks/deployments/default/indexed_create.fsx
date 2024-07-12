#load "../../scripts/ScriptEnv.fsx"
open System.IO
open FsOpenAI.Shared
open FsOpenAI.GenAI
open FSharp.Control
open ScriptEnv

//'install' settings so secrets are available to script code
let baseSettingsFile = @"%USERPROFILE%/.fsopenai/poc/ServiceSettings.json"
ScriptEnv.installSettings baseSettingsFile

//Notes: 
// - settings should contain an endpoint for Azure AI Search and OpenAI API key
// - Azure offers a free tier for AI Search that can be used for this purpose

let docFolder = @"E:\s\genai" //folder with pdfs to index

//create citations.csv index file so hyperlinks to pdf pages can work - this is a requirement for the app
let pdfs = Directory.GetFiles(docFolder, "*.pdf")
let citations = pdfs |> Seq.map (fun f -> $"file:///{f},{Path.GetFileName(f)}") |> Seq.toList
do 
    ["Link,Document"]
    @ citations
    |> fun lines -> File.WriteAllLines (Path.Combine(docFolder, "citations.csv"),lines)

//shred and index the pdfs
let embModel = "text-embedding-ada-002"
let shredded = ScriptEnv.Indexes.shredPdfsAsync docFolder
let embedded = ScriptEnv.Indexes.getEmbeddingsAsync embModel ScriptEnv.openAiClient 7.0 shredded
let searchDocs = embedded |> AsyncSeq.map ScriptEnv.Indexes.toSearchDoc
let indexDef = ScriptEnv.Indexes.indexDefinition "sample-index"
ScriptEnv.Indexes.loadIndexAsync true indexDef searchDocs |> Async.Start

