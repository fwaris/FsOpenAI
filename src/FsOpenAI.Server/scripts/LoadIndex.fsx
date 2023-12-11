#load "Env.fsx"
open FSharp.Control
(*
Example script that loads a collection of PDF documents in a local folder
to an Azure vector search index
*)

Env.installSettings "%USERPROFILE%/.fsopenai/poc/ServiceSettings.json"

let shredded = Env.Index.shredPdfsAsync @"C:\s\genai\gaap"
let embedded = Env.Index.getEmbeddingsAsync 7.0 shredded
let searchDocs = embedded |> AsyncSeq.map Env.Index.toSearchDoc
let indexDef = Env.Index.indexDefinition "gaap"
Env.Index.loadIndexAsync true indexDef searchDocs |> Async.Start

