#r "nuget: Azure.AI.OpenAI, *-*"
#r "nuget: FSharp.Control.AsyncSeq"

open System
open FSharp.Control
open OpenAI

//Shows how to get streaming content for chat completions

let client = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
let chatc = client.GetChatClient("gpt-4o-mini")

let m1 = Chat.ChatMessage.CreateUserMessage("What is the meaning of life?")

let resp = chatc.CompleteChatStreamingAsync(m1) 
let gs =
    let mutable ls = []
    resp
    |> AsyncSeq.ofAsyncEnum
    |> AsyncSeq.collect(fun cs -> AsyncSeq.ofSeq cs.ContentUpdate)
    |> AsyncSeq.map(fun x -> x.Text)
    |> AsyncSeq.iter(fun x-> printfn "%A" x; ls<-x::ls)
    |> Async.RunSynchronously
    String.concat "" (List.rev ls)
printf "%s" gs