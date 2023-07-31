#r "nuget: Azure.AI.OpenAI, 1.0.0-beta.6"
#r "nuget: FSharp.Control.AsyncSeq"
open System
open Azure.AI.OpenAI
open FSharp.Control

let asAsyncSeq<'t> (xs:System.Collections.Generic.IAsyncEnumerable<'t>) = 
    asyncSeq {
        let mutable hs = false
        let xs = xs.GetAsyncEnumerator()
        let! hasNext = task{return! xs.MoveNextAsync()} |> Async.AwaitTask
        hs <- hasNext
        while hs do
            yield xs.Current
            let! hasNext = task{return! xs.MoveNextAsync()} |> Async.AwaitTask
            hs <- hasNext
        xs.DisposeAsync() |> ignore
    }

let clint = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))

let m1 = new ChatMessage(role=ChatRole.User, content= "What is the meaning of life?")

let resp = clint.GetChatCompletionsStreamingAsync("gpt-3.5-turbo",ChatCompletionsOptions([m1])) |> Async.AwaitTask |> Async.RunSynchronously
let rs = resp.Value.GetChoicesStreaming()
let gs =
    let mutable ls = []
    rs
    |> asAsyncSeq
    |> AsyncSeq.collect(fun cs ->  AsyncSeq.ofAsyncEnum (cs.GetMessageStreaming()) |> AsyncSeq.map(fun m -> cs.Index,m) )
    |> AsyncSeq.map(fun(i,x) -> x)
    |> AsyncSeq.iter(fun x-> ls<-x::ls)
    |> Async.RunSynchronously
    ls
        

let r1 = clint.GetChatCompletions("gpt-3.5-turbo",ChatCompletionsOptions([m1]))
let d1 = clint.GetCompletions("gpt-3.5-turbo",CompletionsOptions(["write a story about the happy life of a beautiful butterfly"]))


