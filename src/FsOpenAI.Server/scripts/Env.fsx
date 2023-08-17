#load "packages.fsx"
open System
open System.Threading
open System.Threading.Tasks
open Azure
open Azure.Search.Documents.Indexes
open Azure.AI.OpenAI
open FSharp.Control

let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

let modelDimensions = 1536; //dimensions for gpt3 based embedding model
let rng = Random()
let randSelect (ls:_ list) = ls.[rng.Next(ls.Length)]

let settingsFile = 
    let fn = @"%USERPROFILE%\.fsopenai\ServiceSettings.json"
    Environment.ExpandEnvironmentVariables(fn)


let settings = 
    System.Text.Json.JsonSerializer.Deserialize<FsOpenAI.Client.ServiceSettings>(System.IO.File.ReadAllText settingsFile)

let openAIEndpoint = randSelect settings.AZURE_OPENAI_ENDPOINTS
let searchEndpoint = randSelect settings.AZURE_SEARCH_ENDPOINTS

//let srchIndex = sMap.["AZURE_SEARCH_INDEX_NAME"]
let srchAdmKey = searchEndpoint.API_KEY
let srchEndpoint = searchEndpoint.ENDPOINT
let openAiEndpoint = $"https://{openAIEndpoint.RESOURCE_GROUP}.openai.azure.com"
let openApiKey = openAIEndpoint.API_KEY
let embModel = settings.AZURE_OPENAI_MODELS.Value.EMBEDDING.[0]
let chatModel = settings.AZURE_OPENAI_MODELS.Value.CHAT.[2]

let srchCred = AzureKeyCredential(srchAdmKey)
let openAiCred = AzureKeyCredential(openApiKey)
let indexClient = SearchIndexClient(Uri srchEndpoint,srchCred)
let openAiClient = OpenAIClient(Uri openAiEndpoint,openAiCred)

let inline await fn = fn |> Async.AwaitTask |> Async.RunSynchronously

let inline awaitList asyncEnum = asyncEnum |> AsyncSeq.ofAsyncEnum |> AsyncSeq.toBlockingSeq |> Seq.toList

let asyncShow a = 
    async {
        let! (r:Microsoft.SemanticKernel.Orchestration.SKContext) = a
        printfn "Answer:"
        printfn "%A" r.Variables.Input
    }
    |> Async.Start

module Async =
   let map f a = async.Bind(a, f >> async.Return)

module AsyncSeq =
    let mapAsyncParallelThrottled (parallelism:int) (f:'a -> Async<'b>) (s:AsyncSeq<'a>) : AsyncSeq<'b> = asyncSeq {
        use mb = MailboxProcessor.Start (ignore >> async.Return)
        use sm = new SemaphoreSlim(parallelism)
        let! err =
            s
            |> AsyncSeq.iterAsync (fun a -> async {
            let! _ = sm.WaitAsync () |> Async.AwaitTask
            let! b = Async.StartChild (async {
                try return! f a
                finally sm.Release () |> ignore })
            mb.Post (Some b) })
            |> Async.map (fun _ -> mb.Post None)
            |> Async.StartChildAsTask
        yield!
            AsyncSeq.unfoldAsync (fun (t:Task) -> async{
            if t.IsFaulted then 
                return None
            else 
                let! d = mb.Receive()
                match d with
                | Some c -> 
                    let! d' = c
                    return Some (d',t)
                | None -> return None
            })
            err
    }
(* 
      //implementation possible within AsyncSeq, with the supporting code available there       
      let mapAsyncParallelThrottled (parallelism:int) (f:'a -> Async<'b>) (s:AsyncSeq<'a>) : AsyncSeq<'b> = asyncSeq {
        use mb = MailboxProcessor.Start (ignore >> async.Return)
        use sm = new SemaphoreSlim(parallelism)
        let! err =
          s
          |> iterAsync (fun a -> async {
            do! sm.WaitAsync () |> Async.awaitTaskUnitCancellationAsError
            let! b = Async.StartChild (async {
              try return! f a
              finally sm.Release () |> ignore })
            mb.Post (Some b) })
          |> Async.map (fun _ -> mb.Post None)
          |> Async.StartChildAsTask
        yield!
          replicateUntilNoneAsync (Task.chooseTask (err |> Task.taskFault) (async.Delay mb.Receive))
          |> mapAsync id }
*)
    let mapAsyncParallelRateLimit (opsPerSecond:float) (f:'a -> Async<'b>) (s:AsyncSeq<'a>) : AsyncSeq<'b> = asyncSeq {
        let mutable tRef = DateTime.Now
        use mb = MailboxProcessor.Start (ignore >> async.Return)
        let mutable l = 0L
        let incr() = Interlocked.Increment(&l)
        let! err =
            s
            |> AsyncSeq.iterAsync (fun a -> async {
                let l' = incr() |> float
                let elapsed = (DateTime.Now - tRef).TotalSeconds 
                let rate = l' / elapsed |> min (2.0 * opsPerSecond)
                if elapsed > 60. then
                    tRef <- DateTime.Now
                    l <- 0
                printfn $"rate {rate}"
                let diffRate = rate - opsPerSecond
                if diffRate > 0 then
                    do! Async.Sleep ( int (diffRate * 1000.0))
                let! b = Async.StartChild (async {                    
                    try return! f a
                    finally () })
                mb.Post (Some b) })
            |> Async.map (fun _ -> mb.Post None)
            |> Async.StartChildAsTask
        yield!
            AsyncSeq.unfoldAsync (fun (t:Task) -> async{
            if t.IsFaulted then 
                return None
            else 
                let! d = mb.Receive()
                match d with
                | Some c -> 
                    let! d' = c
                    return Some (d',t)
                | None -> return None
            })
            err
    }
