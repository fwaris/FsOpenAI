module FsOpenAI.GenAI.Tokens
open System
open Microsoft.ML.Tokenizers
open FsOpenAI.Shared
open FsOpenAI.GenAI.Models
                
[<RequireQualifiedAccess>]  
module Tokens = 
    let tokenSize (s:string) =
        let tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o")
        let tokens,_ = tokenizer.EncodeToTokens(s)
        float tokens.Count

    let msgRole (m:InteractionMessage) = if m.IsUser then "User" else "Assistant"
       
    let tokenEstimateMessages (msgs:InteractionMessage seq) =
        let xs =
            seq {
                for m in msgs do
                    yield $"[{msgRole m}]"
                    yield m.Message
            }
        String.Join("\n",xs)
        |> tokenSize

    let tokenEstimate ch =
        let xs =
            seq {
                yield "[System]"
                yield ch.SystemMessage
                for m in ch.Messages do
                    yield $"[{msgRole m}]"
                    yield m.Message
            }
        String.Join("\n",xs)
        |> tokenSize
        
    let tokenBudget modelsConfig ch =
        Models.chatModels modelsConfig ch.Parameters.Backend
        |> List.map (_.TokenLimit) |> List.max |> float            

