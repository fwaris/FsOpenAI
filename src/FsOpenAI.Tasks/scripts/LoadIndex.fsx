#load "ScriptEnv.fsx"
open FSharp.Control
(*
Example script that loads a collection of PDF documents in a local folder
to an Azure vector search index
*)

ScriptEnv.installSettings "%USERPROFILE%/.fsopenai/ServiceSettings.json"

let shredded = ScriptEnv.Indexes.shredHtmlAsync @"C:\s\glean\plans"
let embedded = ScriptEnv.Indexes.getEmbeddingsAsync 7.0 shredded
let searchDocs = embedded |> AsyncSeq.map ScriptEnv.Indexes.toSearchDoc
let indexDef = ScriptEnv.Indexes.indexDefinition "plans"
ScriptEnv.Indexes.loadIndexAsync true indexDef searchDocs |> Async.Start


//let docs =  Env.Index.shredHtmlAsync @"C:\s\glean\plans" |> AsyncSeq.toBlockingSeq |> Seq.toList
