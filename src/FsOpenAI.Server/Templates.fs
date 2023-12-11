namespace FsOpenAI.Server.Templates
open System.IO
open FSharp.Control
open FsOpenAI.Client
open FsOpenAI.Server
open Microsoft.SemanticKernel

module Templates = 

    let toTemplates p (plugin:IKernelPlugin) = 
        plugin
        |> Seq.map (fun fn -> 
            let fnq = Path.Combine(p,fn.Name,fn.Metadata.PluginName,"question.txt")
            let question = if File.Exists fnq then File.ReadAllText fnq |> Some else None
            {
                Name = fn.Name
                Description = fn.Description
                Template = File.ReadAllText(Path.Combine(p,fn.Metadata.PluginName,fn.Metadata.Name,"skprompt.txt"))
                Question = question
            }
        )
        |> Seq.toList

    let loadSkills p =
        let k = KernelBuilder().Build()
        let dqSkill = k.ImportPluginFromPromptDirectory(p,C.TMPLTS_DOCQUERY)
        let dqTemplates = toTemplates p dqSkill
        let extSkill = k.ImportPluginFromPromptDirectory(p,C.TMPLTS_EXTRACTION)
        let extTemplates = toTemplates p extSkill
        let label= Path.GetFileName p
        {
            Label = label
            Templates = [DocQuery, dqTemplates; Extraction, extTemplates] |> Map.ofList
        }
                
    let loadTemplates() =
        task {            
            let path = Path.Combine(Env.wwwRootPath(),C.TEMPLATES_ROOT)
            let dirs = Directory.GetDirectories(path)
            let templates = dirs |> Seq.map loadSkills |> Seq.toList
            return templates
        }
