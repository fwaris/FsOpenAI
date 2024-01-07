namespace FsOpenAI.Client
open System
open FSharp.Control
open FsOpenAI.Client
open FsOpenAI.Client.Interactions
open Microsoft.SemanticKernel.Connectors.OpenAI

module Completions =
    open Microsoft.SemanticKernel
    type ChatMsg = Azure.AI.OpenAI.ChatRequestMessage 
    type SysMsg = Azure.AI.OpenAI.ChatRequestSystemMessage 
    type AsstMsg = Azure.AI.OpenAI.ChatRequestAssistantMessage
    type UserMsg = Azure.AI.OpenAI.ChatRequestUserMessage
    type ApiRole = Azure.AI.OpenAI.ChatRole 

    let buildCall parms (modelRefs:ModelRef list) ch =
        let sysMsg = Interaction.systemMessage ch
        let messages  = 
            seq {
                if String.IsNullOrWhiteSpace sysMsg |> not then
                    yield SysMsg(sysMsg) :> ChatMsg
                for m in Interaction.messages ch do
                    if not <| Utils.isEmpty m.Message then 
                        yield 
                            match m.Role with 
                            | User -> UserMsg(m.Message)
                            | Assistant _ -> AsstMsg(m.Message)
            }
        let modelRef = messages |> GenUtils.tokenEstimate |> GenUtils.optimalModel modelRefs
        let caller =  GenUtils.getClient parms ch
        let opts = Azure.AI.OpenAI.ChatCompletionsOptions(modelRef.Model,messages)
        opts.Temperature <- GenUtils.temperature ch.Parameters.Mode 
        opts.MaxTokens <- ch.Parameters.MaxTokens
        caller,opts        
        
    let streamChat parms modelsConfig ch =
        async {
            let caller,opts = buildCall parms modelsConfig ch
            let! resp = caller.GetChatCompletionsStreamingAsync(opts) |> Async.AwaitTask                        
            let rs = resp.EnumerateValues() 
            return 
                AsyncSeq.ofAsyncEnum rs
                |> AsyncSeq.map(fun cs -> cs.ChoiceIndex, cs.ContentUpdate)
                |> AsyncSeq.filter(fun (i,x) -> i.HasValue &&  x <> null)
        }        

    let streamCompleteFunction 
        (ch:Interaction)
        (resultSeq : Collections.Generic.IAsyncEnumerable<StreamingKernelContent>) 
        dispatch 
        =
        async {
            let comp = 
                resultSeq
                |> AsyncSeq.ofAsyncEnum
                |> AsyncSeq.map(fun x -> x :?> OpenAIStreamingChatMessageContent)
                |> AsyncSeq.map(fun cs -> cs.ChoiceIndex, cs.Content)
                |> AsyncSeq.filter(fun (i,x) -> x <> null)                
                |> AsyncSeq.bufferByCountAndTime 1 C.CHAT_RESPONSE_TIMEOUT
                |> AsyncSeq.collect(fun xs -> if xs.Length > 0 then AsyncSeq.ofSeq xs else failwith C.TIMEOUT_MSG)
                |> AsyncSeq.iter(fun(i,x) -> dispatch(Srv_Ia_Delta(ch.Id, i, x)))
            match! Async.Catch comp with 
            | Choice1Of2 _ -> dispatch (Srv_Ia_Done(ch.Id,None))
            | Choice2Of2 ex -> dispatch (Srv_Ia_Done(ch.Id,Some ex.Message))
        }

    let streamCompleteChat (parms:ServiceSettings) modelsConfig (ch:Interaction) dispatch = 
       async {
            let modelRefs = GenUtils.chatModels modelsConfig ch.Parameters.Backend
            let! resps = streamChat parms modelRefs ch 
            let comp =               
                resps 
                |> AsyncSeq.bufferByCountAndTime 1 C.CHAT_RESPONSE_TIMEOUT
                |> AsyncSeq.collect(fun xs -> if xs.Length > 0 then AsyncSeq.ofSeq xs else failwith C.TIMEOUT_MSG)
                |> AsyncSeq.iter(fun(i,x) -> dispatch(Srv_Ia_Delta(ch.Id, i.Value, x)))
            match! Async.Catch comp with 
            | Choice1Of2 _ -> dispatch (Srv_Ia_Done(ch.Id,None))
            | Choice2Of2 ex -> dispatch (Srv_Ia_Done(ch.Id,Some ex.Message))
        }

    let completeChat parms modelsConfig ch =
        async {
            let caller,opts = buildCall parms modelsConfig ch
            let! resp = caller.GetChatCompletionsAsync(opts) |> Async.AwaitTask
            return resp.Value.Choices.[0].Message
        }
