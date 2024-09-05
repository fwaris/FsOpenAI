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


let createCitations folder = 
    //create citations.csv index file so hyperlinks to pdf pages can work - this is a requirement for the app
    let pdfs = Directory.GetFiles(folder, "*.pdf")
    let citations = pdfs |> Seq.map (fun f -> $"file:///{f},{Path.GetFileName(f)}") |> Seq.toList
    do 
        ["Link,Document"]
        @ citations
        |> fun lines -> File.WriteAllLines (Path.Combine(folder, "citations.csv"),lines)

//shred and index the pdfs
let embModel = "text-embedding-ada-002"
let clientFac() = ScriptEnv.openAiEmbeddingClient embModel

let indexFolder indexName path =
    createCitations path
    let indexDef = ScriptEnv.Indexes.indexDefinition indexName
    ScriptEnv.Indexes.shredPdfsAsync path
    |> ScriptEnv.Indexes.getEmbeddingsAsync clientFac 7.0
    |> AsyncSeq.map ScriptEnv.Indexes.toSearchDoc
    |> ScriptEnv.Indexes.loadIndexAsync true indexDef 

//ai papers
let docFolder = @"E:\s\genai\papers" //folder with pdfs to index

//ML books
let b1 = @"E:\s\genai\bishop"
let b2 = @"E:\s\genai\mml"


(*
indexFolder "genai-papers" docFolder |> Async.RunSynchronously
indexFolder "pattern-recognition" b1 |> Async.Start
indexFolder "ml-math" b2 |> Async.Start
*) 

//ScriptEnv.Indexes.shredPdfsAsync b1 |> AsyncSeq.take 10 |> AsyncSeq.toBlockingSeq |> Seq.iter (fun x -> printfn "%A" x)


