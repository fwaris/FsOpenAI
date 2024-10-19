namespace FsOpenAI.GenAI
open System
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open Microsoft.SemanticKernel.Connectors.OpenAI

module Completions =
    open Microsoft.SemanticKernel
    ///Construct a call to LLM service (but not invoke it yet, as the results may be streamed later)
    let buildCall parms (invCtx:InvocationContext) ch modelSelector =
        let modelSelector = defaultArg modelSelector (GenUtils.getModels ch.Parameters)
        let modelRefs = modelSelector invCtx ch.Parameters.Backend
        let messages = GenUtils.toChatHistory ch
        let tokenEstimate = GenUtils.tokenEstimate ch
        let modelRef = GenUtils.optimalModel modelRefs tokenEstimate
        let caller,resource =  GenUtils.getClient parms ch modelRef.Model
        let opts = OpenAIPromptExecutionSettings()
        match ch.Parameters.ModelType with 
        | MT_Logic -> 
            opts.MaxTokens <- ch.Parameters.MaxTokens + int tokenEstimate
        | MT_Chat -> 
            opts.MaxTokens <- ch.Parameters.MaxTokens
            opts.Temperature <- float <| GenUtils.temperature ch.Parameters.Mode
        opts.User <- GenUtils.userAgent invCtx
        let de = GenUtils.diaEntryChat ch invCtx modelRef.Model resource
        caller,messages,opts,de

    ///Stream complete chat. Returns async seq of chat completion responses
    let streamChat parms (invCtx:InvocationContext) ch modelSelector =
        async {
            let caller,msgs,opts,de = buildCall parms invCtx ch modelSelector
            let resp = caller.GetStreamingChatMessageContentsAsync(msgs,executionSettings=opts) 
            let xs =
                resp
                |> AsyncSeq.ofAsyncEnum
                |> AsyncSeq.map(fun cs -> cs.ChoiceIndex, cs.Content)
                |> AsyncSeq.filter(fun (i,x) -> x <> null)
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

    let private streamCompleteChat (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch modelSelector =
        async {
            let comp =
               async {
                    let! de,resps = streamChat parms invCtx ch modelSelector
                    Srv_Ia_Notification(ch.Id,$"using model: {de.Model}") |> dispatch
                    let mutable rs = []
                    let comp =
                        resps
                        |> AsyncSeq.bufferByCountAndTime 1 C.CHAT_RESPONSE_TIMEOUT
                        |> AsyncSeq.collect(fun xs -> if xs.Length > 0 then AsyncSeq.ofSeq xs else failwith C.TIMEOUT_MSG)
                        |> AsyncSeq.bufferByCountAndTime 10 1000
                        |> AsyncSeq.filter(fun xs -> xs.Length > 0)                        
                        |> AsyncSeq.map(fun xs -> xs |> Seq.last |> fst, xs |> Seq.map snd |> String.concat "")
                        |> AsyncSeq.iter (fun (i,x) -> rs<-x::rs; dispatch(Srv_Ia_Delta(ch.Id, i, x)))
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

    let completeChat parms invCtx ch dispatch modelSelector =
        async {
            let caller,msgs,opts,de = buildCall parms invCtx ch modelSelector
            try
                Srv_Ia_Notification(ch.Id,$"using model: {de.Model}") |> dispatch
                let! resp = caller.GetChatMessageContentsAsync(msgs,opts) |> Async.AwaitTask
                let respMsg = resp.[0]
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

    let private completeLogicChat (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch modelSelector =
        async {
            let caller,msgs,opts,de = buildCall parms invCtx ch modelSelector
            try
                Srv_Ia_Notification(ch.Id,$"using model: {de.Model}") |> dispatch
                let! resp = caller.GetChatMessageContentsAsync(msgs,opts) |> Async.AwaitTask
                let respMsg = resp.[0]
                match respMsg.Metadata.TryGetValue("FinishReason") with
                | true,n when n = "Length" && Utils.isEmpty respMsg.Content-> 
                    let msg = "No model output due to output token limit. Increase max tokens in chat settings"
                    return failwith msg
                | _ -> ()
                do! 
                    respMsg.Content
                    |> AsyncSeq.ofSeq
                    |> AsyncSeq.bufferByCountAndTime 1000 500
                    |> AsyncSeq.indexed
                    |> AsyncSeq.iter (fun (i,xs) -> dispatch(Srv_Ia_Delta(ch.Id,int i,String(xs))))
                dispatch (Srv_Ia_Done(ch.Id,None))
                let de =
                    {de with
                        Response = respMsg.Content
                        OutputTokens = GenUtils.tokenSize respMsg.Content |> int
                    }
                Monitoring.write (Diag de)
                Srv_Ia_SetSubmissionId(ch.Id,de.id) |> dispatch            
            with ex ->
                Monitoring.write (Diag {de with Error = ex.Message})
                return raise ex
        }

    let checkStreamCompleteChat (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch modelSelector =
        match ch.Parameters.ModelType with
        | MT_Logic -> completeLogicChat parms invCtx ch dispatch modelSelector
        | MT_Chat -> streamCompleteChat parms invCtx ch dispatch modelSelector
