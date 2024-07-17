#load "../../FsOpenAI.Tasks/scripts/ScriptEnv.fsx"
#load "../../FsOpenAI.CodeEvaluator/Prompts.fs"
#load "../../FsOpenAI.CodeEvaluator/CodeEvaluator.fs"
#load "../../FsOpenAI.TravelSurvey.Types/Types.fs"
#load "../../FsOpenAI.TravelSurvey.Types/Loader.fs"
#load "../../FsOpenAI.TravelSurvey.Types/Data.fs"
#load "../../FsOpenAI.TravelSurvey.Qna/Prompts.fs"
#load "../../FsOpenAI.TravelSurvey.Qna/QnA.fs"

open System
open FSharp.Control
open System.IO
open FSharp.CosmosDb
open FsOpenAI.Shared.Interactions
open FsOpenAI.Shared
open FsOpenAI.GenAI
open FsOpenAI.CodeEvaluator.CodeEval

let clearLog() =
    let db() =
        Cosmos.fromConnectionString ScriptEnv.settings.Value.LOG_CONN_STR.Value
        |> Cosmos.database "codegen"
        |> Cosmos.createDatabaseIfNotExists
        |> Cosmos.execAsync
        |> AsyncSeq.iter (printfn "%A")
        |> Async.RunSynchronously
    let container() =
        Cosmos.fromConnectionString ScriptEnv.settings.Value.LOG_CONN_STR.Value
        |> Cosmos.database "codegen"
        |> Cosmos.container "log"
        |> Cosmos.deleteContainerIfExists
        |> Cosmos.execAsync
        |> Async.ignore
        |> Async.RunSynchronously
    db()
    container()

(*
clearLog()
*)

let invCtx = InvocationContext.Default
ScriptEnv.installSettings @"%USERPROFILE%\.fsopenai/poc/ServiceSettings.json"
let settings = ScriptEnv.settings.Value
Monitoring.init(ScriptEnv.settings.Value.LOG_CONN_STR.Value,C.DFLT_COSMOSDB_NAME,"codegen")

let fsiFile = __SOURCE_DIRECTORY__ + "/../../FsOpenAI.TravelSurvey.Types/Types.fs"
let fsiTypes = File.ReadAllLines fsiFile |> Seq.skip 2 |> String.concat "\n"

let preamble = 
    let dllRef = __SOURCE_DIRECTORY__ + "/../../FsOpenAI.TravelSurvey.Types/bin/Debug/net8.0/FsOpenAI.TravelSurvey.Types.dll"
    let dllRef = Path.GetFullPath(dllRef)
    $"""
#r "nuget: Fsharp.Data.Csv.Core"
#r @"{dllRef}"
open FsOpenAI.TravelSurvey.Types
open Helpers

module Data = 
   let load() : Lazy<FsOpenAI.TravelSurvey.Types.DataSets> =  
    lazy(FsOpenAI.TravelSurvey.Data.loadFromPath  @"E:\s\nhts\csv")

"""

let saveCode code =
    let path = __SOURCE_DIRECTORY__ + "/generatedCodeEval.fsx"
    printfn "+++++++++++++++++ Saving code to %s" path
    let text = [preamble; "/*************"; code] |> String.concat "\n"
    File.WriteAllText(path, text)

let dispatch (m:ServerInitiatedMessages) =
    match m with
    | ServerInitiatedMessages.Srv_Ia_SetCode(_,code) -> code |> Option.iter saveCode
    | _ -> ()

let regenPrompt = FsOpenAI.CodeEvaluator.Prompts.fixCodePrompt fsiTypes
let evalParms = {CodeEvalParms.Default with Preamble = preamble; RegenPrompt=regenPrompt; }
;;

let questions =  
    [
    "What is the average commute time for workers in the United States?"
    "What percentage of times people carpool together for work?"
    "What percentage of households own electric vehicles (EVs)?"
    "What are the most common reasons for travel during weekends?"
    "What modes of transportation do college students use to get to campus?. Rank each by share"
    "What percentage of households have Ford vehicles?"
    //
    "What is the percentage of trips by vehicle type?"
    "What percentage of persons used rideshare in the last 30 days. Present the data by Census region." 
    "What is the distribution of riders per trip?"
    "What is the average length of trips by mode of transportation?"
    "What percentage of the trips are loop trips?"
    "What is the average amount paid for parking per trip by census region? Calclulate only for trips where parking was paid"
    "What is the max amount paid for parking per trip by census region?"
    ]

let question = questions.[1]

let chPlan =
    Interaction.create InteractionCreateType.Crt_Plain OpenAI None
    |> snd
    |> Interaction.setSystemMessage (FsOpenAI.TravelSurvey.Prompts.planSysMessage fsiTypes)
    |> Interaction.setUserMessage question

let plan = Completions.completeChat settings invCtx chPlan None dispatch |> ScriptEnv.runA
;;

printfn "%s" plan.Content

let chCode =
    let codePrompt = FsOpenAI.TravelSurvey.Prompts.codePrompt question plan.Content fsiFile
    Interaction.create InteractionCreateType.Crt_Plain OpenAI None
    |> snd
    |> Interaction.setSystemMessage (FsOpenAI.TravelSurvey.Prompts.codeSysMessage plan.Content)
    |> Interaction.setUserMessage codePrompt

let resp = Evaluation.genAndEvalTest ScriptEnv.settings.Value invCtx chCode evalParms dispatch  |> ScriptEnv.runA

printfn "%s" resp
