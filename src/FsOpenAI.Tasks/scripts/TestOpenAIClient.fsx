#r "nuget: Azure.AI.OpenAI, *-*"
#r "nuget: FSharp.Control.AsyncSeq"
open System
open Azure.AI.OpenAI
open FSharp.Control

//Shows how to get streaming content for chat completions

let clint = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))

let m1 = new ChatRequestUserMessage( content= "What is the meaning of life?")

let opts = ChatCompletionsOptions(deploymentName="gpt-3.5-turbo", messages=[m1])

let resp = clint.GetChatCompletionsStreamingAsync(opts) |> Async.AwaitTask |> Async.RunSynchronously
let rs = resp.EnumerateValues()
let gs =
    let mutable ls = []
    rs
    //|> asAsyncSeq
    |> AsyncSeq.ofAsyncEnum
    |> AsyncSeq.map(fun cs ->cs.ChoiceIndex,cs.ContentUpdate)
    |> AsyncSeq.map(fun(i,x) -> x)
    |> AsyncSeq.iter(fun x-> printfn "%A" x; ls<-x::ls)
    |> Async.RunSynchronously
    ls
        


