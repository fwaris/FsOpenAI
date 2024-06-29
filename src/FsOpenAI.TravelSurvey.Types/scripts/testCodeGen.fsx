#load "../../FsOpenAI.Tasks/scripts/ScriptEnv.fsx"
#load "../../FsOpenAI.CodeEvaluator/Prompts.fs"
#load "../../FsOpenAI.CodeEvaluator/CodeEvaluator.fs"
#load "../../FsOpenAI.TravelSurvey.Types/Types.fs"
#load "../../FsOpenAI.TravelSurvey.Types/Loader.fs"
#load "../../FsOpenAI.TravelSurvey.Types/Data.fs"
#load "../../FsOpenAI.TravelSurvey.Qna/Prompts.fs"
#load "../../FsOpenAI.TravelSurvey.Qna/QnA.fs"

open System
open System.IO
open FsOpenAI.Shared.Interactions
open FsOpenAI.Shared
open FsOpenAI.GenAI
open FsOpenAI.CodeEvaluator.CodeEval

let invCtx = InvocationContext.Default
ScriptEnv.installSettings @"%USERPROFILE%\.fsopenai/openai/ServiceSettings.json"
let settings = ScriptEnv.settings.Value

let fsiFile = __SOURCE_DIRECTORY__ + "/../../FsOpenAI.TravelSurvey.Types/Types.fs"
let fsiTypes = File.ReadAllLines fsiFile |> Seq.skip 2 |> String.concat "\n"

let preamble = 
    let dllRef = __SOURCE_DIRECTORY__ + "/../../FsOpenAI.TravelSurvey.Types/bin/Debug/net8.0/FsOpenAI.TravelSurvey.Types.dll"
    let dllRef = Path.GetFullPath(dllRef)
    $"""
#r "nuget: Fsharp.Data.Csv.Core"
#r @"{dllRef}"

module Data = 
   let load() : Lazy<FsOpenAI.TravelSurvey.Types.DataSets> =  
    lazy(FsOpenAI.TravelSurvey.Data.loadFromPath  @"E:\s\nhts\csv")
"""

let regenPrompt = FsOpenAI.CodeEvaluator.Prompts.fixCodePrompt fsiTypes

let questions = 
    [
    "What is the average commute time for workers in the United States?"
    "How often do people carpool for their daily commutes?"
    "What percentage of households own electric vehicles (EVs)?"
    "What are the most common reasons for travel during weekends?"
    "What modes of transportation do college students use to get to campus?"
    "What percentage of households have Ford vechicles?"
    ]

let question = questions.[2]

let chPlan =
    Interaction.create InteractionCreateType.Crt_Plain OpenAI None
    |> snd
    |> Interaction.setSystemMessage (FsOpenAI.TravelSurvey.Prompts.planSysMessage fsiTypes)
    |> Interaction.setUserMessage question

let plan = Completions.completeChat settings invCtx chPlan None |> ScriptEnv.runA
printfn "%s" plan.Content

let chCode =
    let codePrompt = FsOpenAI.TravelSurvey.Prompts.codePrompt question plan fsiFile
    Interaction.create InteractionCreateType.Crt_Plain OpenAI None
    |> snd
    |> Interaction.setSystemMessage FsOpenAI.TravelSurvey.Prompts.codeSysMessage
    |> Interaction.setUserMessage codePrompt


let evalParms = {CodeEvalParms.Default with Preamble = preamble; RegenPrompt=regenPrompt; }

Evaluation.genAndEvalTest ScriptEnv.settings.Value invCtx chCode evalParms  |> ScriptEnv.runA
