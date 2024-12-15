namespace FsOpenAI.GenAI
open System
open Microsoft.SemanticKernel.Connectors.OpenAI
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.GenAI.Models
open FsOpenAI.GenAI.Tokens
open FsOpenAI.GenAI.Endpoints
open FsOpenAI.GenAI.ChatUtils

module Completions =
    open Microsoft.SemanticKernel
    ///Construct a call to LLM service (but not invoke it yet, as the results may be streamed later)
    let buildCall parms (invCtx:InvocationContext) ch modelSelector (responseFormat:Type option) =
        let modelSelector = defaultArg modelSelector (Models.getModels ch.Parameters)
        let modelRefs = modelSelector invCtx ch.Parameters.Backend
        let modelRef = Models.pick modelRefs
        let messages = ChatUtils.toChatHistory ch
        let caller,resource =  Endpoints.getClient parms ch modelRef.Model
        let opts = OpenAIPromptExecutionSettings()
        match ch.Parameters.ModelType with
        | MT_Logic -> () //o1 preview api may not accept 'normal' settings
            //opts.MaxTokens <- ch.Parameters.MaxTokens + int tokenEstimate
        | MT_Chat ->
            opts.MaxTokens <- ch.Parameters.MaxTokens
            opts.Temperature <- float <| ChatUtils.temperature ch.Parameters.Mode
        opts.User <- GenUtils.userAgent invCtx
        responseFormat |> Option.iter(fun rf -> opts.ResponseFormat <- rf)
        let de = GenUtils.diaEntryChat ch invCtx modelRef.Model resource
        caller,messages,opts,de

    ///Stream complete chat. Returns async seq of chat completion responses
    let streamChat parms (invCtx:InvocationContext) ch modelSelector responseFormat =
        async {
            let caller,msgs,opts,de = buildCall parms invCtx ch modelSelector responseFormat
            let resp = caller.GetStreamingChatMessageContentsAsync(msgs,executionSettings=opts)
            let xs =
                resp
                |> AsyncSeq.ofAsyncEnum
                |> AsyncSeq.map(fun cs -> cs.Content)
                |> AsyncSeq.filter(fun x -> x <> null)
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
                |> AsyncSeq.map(fun cs -> cs.Content)
                |> AsyncSeq.filter(fun x -> x <> null)
                |> AsyncSeq.bufferByCountAndTime 1 C.CHAT_RESPONSE_TIMEOUT
                |> AsyncSeq.collect(fun xs -> if xs.Length > 0 then AsyncSeq.ofSeq xs else failwith C.TIMEOUT_MSG)
                |> AsyncSeq.iter(fun x -> dispatch(Srv_Ia_Delta(ch.Id, x)))
            match! Async.Catch comp with
            | Choice1Of2 _ -> dispatch (Srv_Ia_Done(ch.Id,None))
            | Choice2Of2 ex -> dispatch (Srv_Ia_Done(ch.Id,Some ex.Message))
        }


    ///Stream complete chat. Dispatches chat responses to the client
    let private streamCompleteChat (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch modelSelector =
        async {
            let comp =
               async {
                    let! de,resps = streamChat parms invCtx ch modelSelector None
                    Srv_Ia_Notification(ch.Id,$"using model: {de.Model}") |> dispatch
                    let mutable rs : string list = []
                    let comp =
                        resps
                        |> AsyncSeq.bufferByCountAndTime 1 C.CHAT_RESPONSE_TIMEOUT
                        |> AsyncSeq.collect(fun xs -> if xs.Length > 0 then AsyncSeq.ofSeq xs else failwith C.TIMEOUT_MSG)
                        |> AsyncSeq.bufferByCountAndTime 10 1000
                        |> AsyncSeq.filter(fun xs -> xs.Length > 0)
                        |> AsyncSeq.map(String.concat "")
                        |> AsyncSeq.iter (fun x -> rs<-x::rs; dispatch(Srv_Ia_Delta(ch.Id,x)))
                    match! Async.Catch comp with
                    | Choice1Of2 _ ->
                        let resp = String.Join("",rs |> List.rev)
                        let de =
                            {de with
                                Response = resp
                                OutputTokens = Tokens.tokenSize resp |> int
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
                GenUtils.handleChatException dispatch ch.Id "Completions.streamCompleteChat" ex
        }

    ///<summary>
    ///Enforce a <see cref="AnswerWithCitations" /> json response from LLM but still stream complete the Answer string.
    ///Send citations as a separate message
    ///<br/>As a fallback, If processing fails, reset chat and run <see cref="streamCompleteChat" />
    ///</summary>
    let private streamCompleteChatFormatted (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch modelSelector =
        async {
            let comp =
               async {
                    let responseFormat = Some typeof<AnswerWithCitations>
                    let! de,resps = streamChat parms invCtx ch modelSelector responseFormat
                    Srv_Ia_Notification(ch.Id,$"using model: {de.Model}") |> dispatch
                    let mutable rs : string list = []
                    let cits = ref []
                    let comp =
                        resps
                        |> AsyncSeq.bufferByCountAndTime 1 C.CHAT_RESPONSE_TIMEOUT
                        |> AsyncSeq.collect(fun xs -> if xs.Length > 0 then AsyncSeq.ofSeq xs else failwith C.TIMEOUT_MSG)
                        |> AsyncSeq.bufferByCountAndTime 10 1000
                        |> AsyncSeq.filter(fun xs -> xs.Length > 0)
                        |> AsyncSeq.map(String.concat "")
                        //----- use stream parser -----
                        |> AsyncSeq.scan StreamParser.updateState (StreamParser.exp cits,(StreamParser.State.Empty,[]))
                        |> AsyncSeq.collect (fun (_,(_,os)) -> os |> List.rev |> AsyncSeq.ofSeq)
                        //-----------------------------
                        |> AsyncSeq.iter (fun x -> rs<-x::rs; dispatch(Srv_Ia_Delta(ch.Id,x)))
                    match! Async.Catch comp with
                    | Choice1Of2 _ ->
                        let resp = String.Join("",rs |> List.rev)
                        let de =
                            {de with
                                Response = resp
                                OutputTokens = Tokens.tokenSize resp |> int
                            }
                        Monitoring.write (Diag de)
                        Srv_Ia_SetSubmissionId(ch.Id,de.id) |> dispatch
                        match cits.Value with
                        | [] -> ()
                        | xs -> dispatch (Srv_Ia_Citations(ch.Id,xs))
                        dispatch (Srv_Ia_Done(ch.Id,None))
                    | Choice2Of2 ex ->
                        //fallback
                        dispatch (Srv_Ia_Notification(ch.Id,"Unable to complete chat [no structured output]. Retrying..."))
                        dispatch (Srv_Ia_Reset(ch.Id))
                        do! streamCompleteChat parms invCtx ch dispatch modelSelector
                }
            match! Async.Catch comp with
            | Choice1Of2 _ -> ()
            | Choice2Of2 ex ->
                GenUtils.handleChatException dispatch ch.Id "Completions.streamCompleteChatFormatted" ex
        }

    let completeChat parms invCtx ch dispatch modelSelector responseFormat =
        async {
            let caller,msgs,opts,de = buildCall parms invCtx ch modelSelector responseFormat
            try
                Srv_Ia_Notification(ch.Id,$"using model: {de.Model}") |> dispatch
                let! resp = caller.GetChatMessageContentsAsync(msgs,opts) |> Async.AwaitTask
                let respMsg = resp.[0]
                let de =
                    {de with
                        Response = respMsg.Content
                        OutputTokens = Tokens.tokenSize respMsg.Content |> int
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
            let caller,msgs,opts,de = buildCall parms invCtx ch modelSelector None
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
                    |> AsyncSeq.iter (fun xs -> dispatch(Srv_Ia_Delta(ch.Id,String(xs))))
                dispatch (Srv_Ia_Done(ch.Id,None))
                let de =
                    {de with
                        Response = respMsg.Content
                        OutputTokens = Tokens.tokenSize respMsg.Content |> int
                    }
                Monitoring.write (Diag de)
                Srv_Ia_SetSubmissionId(ch.Id,de.id) |> dispatch
            with ex ->
                Monitoring.write (Diag {de with Error = ex.Message})
                return raise ex
        }

    let checkStreamCompleteChat (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch modelSelector haveCitations =
        match ch.Parameters.ModelType with
        | MT_Logic -> completeLogicChat parms invCtx ch dispatch modelSelector
        | MT_Chat ->
            if haveCitations then
                streamCompleteChatFormatted parms invCtx ch dispatch modelSelector
            else
                streamCompleteChat parms invCtx ch dispatch modelSelector
