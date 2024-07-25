namespace FsOpenAI.GenAI
open System
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open Microsoft.SemanticKernel.Connectors.OpenAI

module Completions =
    open Microsoft.SemanticKernel
    type ChatMsg = Azure.AI.OpenAI.ChatRequestMessage
    type SysMsg = Azure.AI.OpenAI.ChatRequestSystemMessage
    type AsstMsg = Azure.AI.OpenAI.ChatRequestAssistantMessage
    type UserMsg = Azure.AI.OpenAI.ChatRequestUserMessage
    type ApiRole = Azure.AI.OpenAI.ChatRole

    ///Construct a call to LLM service (but not invoke it yet, as the results may be streamed later)
    let buildCall parms (invCtx:InvocationContext) ch modelSelector =
        let modelSelector = defaultArg modelSelector GenUtils.chatModels
        let modelRefs = modelSelector invCtx ch.Parameters.Backend
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
            |> Seq.toList
        let tokenEstimate = GenUtils.tokenEstimate ch
        let modelRef = GenUtils.optimalModel modelRefs tokenEstimate
        let caller,resource =  GenUtils.getClient parms ch
        let opts = Azure.AI.OpenAI.ChatCompletionsOptions(modelRef.Model,messages)
        opts.User <- GenUtils.userAgent invCtx
        opts.Temperature <- GenUtils.temperature ch.Parameters.Mode
        opts.MaxTokens <- ch.Parameters.MaxTokens
        let de = GenUtils.diaEntryChat ch invCtx modelRef.Model resource
        caller,opts,de

    ///Stream complete chat. Returns async seq of chat completion responses
    let streamChat parms (invCtx:InvocationContext) ch modelSelector =
        async {
            let caller,opts,de = buildCall parms invCtx ch modelSelector
            let! resp = caller.GetChatCompletionsStreamingAsync(opts) |> Async.AwaitTask
            let xs =
                resp.EnumerateValues()
                |> AsyncSeq.ofAsyncEnum
                |> AsyncSeq.map(fun cs -> cs.ChoiceIndex, cs.ContentUpdate)
                |> AsyncSeq.filter(fun (i,x) -> i.HasValue &&  x <> null)
                |> AsyncSeq.bufferByCountAndTime 1 C.CHAT_RESPONSE_TIMEOUT
                |> AsyncSeq.collect(fun xs -> if xs.Length > 0 then AsyncSeq.ofSeq xs else failwith C.TIMEOUT_MSG)
            return (de,xs)
        }

    ///Stream complete Semantic Kernel function invocation
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

    let streamCompleteChat (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch modelSelector =
        async {
            let comp =
               async {
                    let! de,resps = streamChat parms invCtx ch modelSelector
                    let mutable rs = []
                    let comp =
                        resps
                        |> AsyncSeq.bufferByCountAndTime 1 C.CHAT_RESPONSE_TIMEOUT
                        |> AsyncSeq.collect(fun xs -> if xs.Length > 0 then AsyncSeq.ofSeq xs else failwith C.TIMEOUT_MSG)
                        |> AsyncSeq.iter(fun(i,x) -> rs<-x::rs; dispatch(Srv_Ia_Delta(ch.Id, i.Value, x)))
                    match! Async.Catch comp with
                    | Choice1Of2 _ ->
                        let resp = String.Join("",rs |> List.rev)
                        let de =
                            {de with
                                Response = resp
                                OutputTokens = GenUtils.tokenSize resp |> int
                            }
                        Monitoring.write (Diag de)
                        Srv_Ia_SetSubmissionId(ch.Id,de.id) |> dispatch
                        dispatch (Srv_Ia_Done(ch.Id,None))
                    | Choice2Of2 ex ->
                        Monitoring.write (Diag {de with Error = ex.Message})
                        dispatch (Srv_Ia_Done(ch.Id,Some ex.Message))
                }
            match! Async.Catch comp with
            | Choice1Of2 _ -> ()
            | Choice2Of2 ex ->
                Env.logException (ex,"streamCompleteChat: ")
                dispatch (Srv_Ia_Done(ch.Id,Some ex.Message))
        }

    let completeChat parms invCtx ch modelSelector dispatch =
        async {
            let caller,opts,de = buildCall parms invCtx ch modelSelector
            try
                let! resp = caller.GetChatCompletionsAsync(opts) |> Async.AwaitTask
                let respMsg = resp.Value.Choices.[0].Message
                let de =
                    {de with
                        Response = respMsg.Content
                        OutputTokens = GenUtils.tokenSize respMsg.Content |> int
                    }
                Monitoring.write (Diag de)
                Srv_Ia_SetSubmissionId(ch.Id,de.id) |> dispatch
                return respMsg
            with ex ->
                Monitoring.write (Diag {de with Error = ex.Message})
                return raise ex
        }

    let completeChatLowcost parms invCtx ch modelSelector dispatch =
        async {
            let caller,opts,de = buildCall parms invCtx ch modelSelector
            try
                let! resp = caller.GetChatCompletionsAsync(opts) |> Async.AwaitTask
                let respMsg = resp.Value.Choices.[0].Message
                let de =
                    {de with
                        Response = respMsg.Content
                        OutputTokens = GenUtils.tokenSize respMsg.Content |> int
                    }
                Monitoring.write (Diag de)
                Srv_Ia_SetSubmissionId(ch.Id,de.id) |> dispatch
                return respMsg
            with ex ->
                Monitoring.write (Diag {de with Error = ex.Message})
                return raise ex
        }
