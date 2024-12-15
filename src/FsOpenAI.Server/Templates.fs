namespace FsOpenAI.Server.Templates
open System.IO
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.GenAI
open Microsoft.SemanticKernel

module Templates = 
    let private (@@) a b = Path.Combine(a,b)

    let toTemplates p (plugin:KernelPlugin) = 
        plugin
        |> Seq.map (fun fn -> 
            let fnq = p @@ fn.Name @@ "question.txt"
            let question = if File.Exists fnq then File.ReadAllText fnq |> Some else None
            {
                Name = fn.Name
                Description = fn.Description
                Template = File.ReadAllText(p @@ fn.Name @@ "skprompt.txt")
                Question = question
            }
        )
        |> Seq.toList

    let loadSkill p skillName =
        let k = Kernel.CreateBuilder().Build()
        let p = Path.Combine(p,skillName)
        let functions = k.ImportPluginFromPromptDirectory(p)
        toTemplates p functions
                
    let loadTemplates() =
        task {            
            let path = Path.Combine(Env.wwwRootPath(),C.TEMPLATES_ROOT.Value)
            let dirs = Directory.GetDirectories(path)
            let templates = 
                dirs
                |> Seq.filter (fun  p -> 
                    let dirs = Directory.GetDirectories p |> Seq.map Path.GetFileName |> Set.ofSeq
                    dirs.Contains(C.TMPLTS_DOCQUERY) && dirs.Contains(C.TMPLTS_EXTRACTION)
                    )
                |> Seq.map (fun d -> 
                    let name = Path.GetFileName d
                    let dqTemplates = loadSkill d C.TMPLTS_DOCQUERY
                    let extTemplates = loadSkill d C.TMPLTS_EXTRACTION
                    {
                        Label = name
                        Templates = [DocQuery, dqTemplates; Extraction, extTemplates] |> Map.ofList
                    }
                )
                |> Seq.toList
            return templates
        }