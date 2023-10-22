namespace FsOpenAI.Server.Templates
open System.Collections.Generic
open System.IO
open FSharp.Control
open FsOpenAI.Client
open FsOpenAI.Server
open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.Plugins

module Templates = 

    let toTemplates p (skill:IDictionary<string,ISKFunction>) = 
        skill
        |> Seq.map (fun kv -> 
            let fv = kv.Value.Describe()            
            let fnq = Path.Combine(p,kv.Value.PluginName,kv.Key,"question.txt")
            let question = if File.Exists fnq then File.ReadAllText fnq |> Some else None
            {
                Name = kv.Key
                Description = kv.Value.Description
                Template = File.ReadAllText(Path.Combine(p,kv.Value.PluginName,kv.Key,"skprompt.txt"))
                Question = question
            }
        )
        |> Seq.toList

    let loadSkills p =
        let k = Kernel.Builder.Build()
        let dqSkill = k.ImportSemanticFunctionsFromDirectory(p,[|C.TMPLTS_DOCQUERY|])
        let dqTemplates = toTemplates p dqSkill
        let extSkill = k.ImportSemanticFunctionsFromDirectory(p,[|C.TMPLTS_EXTRACTION|])
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
