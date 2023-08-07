namespace FsOpenAI.Client
open System
open FSharp.Control
open FsOpenAI.Client
open Azure.AI.OpenAI

module Completions =
    
    type ApiMsg = Azure.AI.OpenAI.ChatMessage // OpenAI_API.Chat.ChatMessage
    type ApiRole = Azure.AI.OpenAI.ChatRole // OpenAI_API.Chat.ChatMessageRole

    let completeChat (parms:ServiceSettings) (ch:Interaction) dispatch = 
        //postDelta postEnd postErr (ch:Interaction) =
       async {
            try             
                let sysMsg = match ch.InteractionType with Chat s -> s | _ -> ""
                let messages = 
                    seq {
                        if String.IsNullOrWhiteSpace sysMsg |> not then
                            yield ApiMsg(ApiRole.System, sysMsg)
                        for m in ch.Messages do
                            let role = 
                                match m.Role with 
                                | User _-> ApiRole.User
                                | Assistant  -> ApiRole.Assistant
                            if String.IsNullOrWhiteSpace m.Message |> not then 
                                yield ApiMsg(role,m.Message)                                 
                    }                
                let caller =  Utils.getClient parms ch
                let opts = Azure.AI.OpenAI.ChatCompletionsOptions(messages)
                opts.Temperature <- float32 ch.Parameters.Temperature
                opts.PresencePenalty <- float32 ch.Parameters.PresencePenalty
                opts.FrequencyPenalty <- float32 ch.Parameters.FrequencyPenalty
                opts.MaxTokens <- ch.Parameters.MaxTokens
                let! resp = caller.GetChatCompletionsStreamingAsync(ch.Parameters.ChatModel,opts) |> Async.AwaitTask                        
                let rs = resp.Value.GetChoicesStreaming()
                printfn "made call"
                do!
                    AsyncSeq.ofAsyncEnum rs
                    |> AsyncSeq.collect(fun cs ->  AsyncSeq.ofAsyncEnum (cs.GetMessageStreaming()) |> AsyncSeq.map(fun m -> cs.Index,m) )
                    |> AsyncSeq.iter(fun(i,x) -> dispatch(Srv_Ia_Delta(ch.Id, i.Value, x.Content)))
                dispatch (Srv_Ia_Done (ch.Id,None))                
            with ex ->
                dispatch (Srv_Ia_Done(ch.Id,Some ex.Message))
        }

