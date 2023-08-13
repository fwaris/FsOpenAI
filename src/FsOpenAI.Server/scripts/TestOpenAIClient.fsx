#r "nuget: Azure.AI.OpenAI, *-*"
#r "nuget: FSharp.Control.AsyncSeq"
open System
open Azure.AI.OpenAI
open FSharp.Control

//Shows how to get streaming content for chat completions

let clint = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))

let m1 = new ChatMessage(role=ChatRole.User, content= "What is the meaning of life?")

let resp = clint.GetChatCompletionsStreamingAsync("gpt-3.5-turbo",ChatCompletionsOptions([m1])) |> Async.AwaitTask |> Async.RunSynchronously
let rs = resp.Value.GetChoicesStreaming()
let gs =
    let mutable ls = []
    rs
    //|> asAsyncSeq
    |> AsyncSeq.ofAsyncEnum
    |> AsyncSeq.collect(fun cs ->  AsyncSeq.ofAsyncEnum (cs.GetMessageStreaming()) |> AsyncSeq.map(fun m -> cs.Index,m) )
    |> AsyncSeq.map(fun(i,x) -> x)
    |> AsyncSeq.iter(fun x-> printfn "%A" x.Content; ls<-x::ls)
    |> Async.RunSynchronously
    ls
        


