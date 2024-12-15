namespace FsOpenAI.Server.Templates
open System.IO
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.GenAI
open System.Text.Json
open System.Text.Json.Serialization

module Samples = 

    let loadSamples() =
        task {            
            let path = Path.Combine(Env.wwwRootPath(),C.TEMPLATES_ROOT.Value)
            let dirs = Directory.GetDirectories(path)
            let samples =
                dirs 
                |> Seq.map (fun d -> 
                    let name = Path.GetFileName d
                    let str = File.ReadAllText (Path.Combine(d,C.SAMPLES_JSON))
                    let vs : SamplePrompt list = JsonSerializer.Deserialize(str,Utils.serOptions())
                    name,vs)
            return samples
        }

