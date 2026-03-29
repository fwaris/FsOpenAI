namespace FsOpenAI.GenAI
open System
open System.Threading
open Microsoft.Extensions.AI
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open FsOpenAI.GenAI.Models
open FsOpenAI.GenAI.Tokens
open FsOpenAI.GenAI.Endpoints
open FsOpenAI.GenAI.ChatUtils

module Completions =
    let noTempModels = set ["gpt-5"]
    let responseText (resp:ChatResponse) =
        if obj.ReferenceEquals(resp,null) then ""
        elif not (String.IsNullOrWhiteSpace resp.Text) then resp.Text
        else
            resp.Messages
            |> Seq.rev
            |> Seq.tryPick(fun m -> if String.IsNullOrWhiteSpace m.Text then None else Some m.Text)
            |> Option.defaultValue ""

    let private updateText (update:ChatResponseUpdate) =
        update.Contents
        |> Seq.choose (function
            | :? TextContent as tc when Utils.notEmpty tc.Text -> Some tc.Text
            | _ -> None)
        |> String.concat ""

    let private updateReasoning (update:ChatResponseUpdate) =
        update.Contents
        |> Seq.choose (function
            | :? TextReasoningContent as tc when Utils.notEmpty tc.Text -> Some tc.Text
            | _ -> None)
        |> String.concat ""

    let private streamingUpdates (updates: AsyncSeq<ChatResponseUpdate>) =
        updates
        |> AsyncSeq.bufferByCountAndTime 1 C.CHAT_RESPONSE_TIMEOUT
        |> AsyncSeq.collect(fun xs -> if xs.Length > 0 then AsyncSeq.ofSeq xs else failwith C.TIMEOUT_MSG)

    ///Construct a call to LLM service (but not invoke it yet, as the results may be streamed later)
    let buildCall parms (invCtx:InvocationContext) ch modelSelector (responseFormat:Type option) =
        let modelSelector = defaultArg modelSelector (Models.getModels ch.Parameters)
        let modelRefs = modelSelector invCtx ch.Parameters.Backend
        let modelRef = Models.pick modelRefs
        let messages = ChatUtils.toChatMessages ch
        let caller =  Endpoints.getClient parms ch modelRef.Model
        let opts = ChatOptions()
        opts.ModelId <- modelRef.Model
        opts.MaxOutputTokens <- Nullable ch.Parameters.MaxTokens
        if not(noTempModels.Contains modelRef.Model) then
            opts.Temperature <- Nullable (ChatUtils.temperature ch.Parameters.Mode)
        if not (isNull opts.AdditionalProperties) then
            opts.AdditionalProperties["user"] <- GenUtils.userAgent invCtx
        responseFormat |> Option.iter(fun rf -> opts.ResponseFormat <- ChatResponseFormat.ForJsonSchema(rf))
        let de = GenUtils.diaEntryChat ch invCtx modelRef.Model (string ch.Parameters.Backend)
        caller,messages,opts,de

    ///Stream complete chat. Returns async seq of chat completion responses
    let streamChat parms (invCtx:InvocationContext) ch modelSelector responseFormat =
        async {
            let caller,msgs,opts,de = buildCall parms invCtx ch modelSelector responseFormat
            let resp = caller.GetStreamingResponseAsync(msgs,opts,CancellationToken.None)
            return (de, resp |> AsyncSeq.ofAsyncEnum)
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
                        |> streamingUpdates
                        |> AsyncSeq.map updateText
                        |> AsyncSeq.filter Utils.notEmpty
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
                        |> streamingUpdates
                        |> AsyncSeq.map updateText
                        |> AsyncSeq.filter Utils.notEmpty
                        |> AsyncSeq.bufferByCountAndTime 10 1000
                        |> AsyncSeq.filter(fun xs -> xs.Length > 0)
                        |> AsyncSeq.map(String.concat "")
                        //----- use stream parser -----
                        |> AsyncSeq.scan StreamParser.updateState (StreamParser.citationsExp cits,(StreamParser.State.Empty,[]))
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
    
    
    let private streamCompleteChatStructured (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch modelSelector =
        async {
            let comp =
               async {                   
                    let mode = Interactions.Interaction.mode ch (Env.allowedModes())
                    let haveCitations = mode.IsM_Doc_Index || mode.IsM_Index
                    let hasThought = ch.Parameters.Backend.BackendType.IsChatCompletionsHarmony
                    let citResponseFromat = if  haveCitations then Some typeof<AnswerWithCitations> else None
                    let! de,resps = streamChat parms invCtx ch modelSelector citResponseFromat
                    Srv_Ia_Notification(ch.Id,$"using model: {de.Model}") |> dispatch
                    let mutable rs : string list = []
                    let cits = ref []
                    let thought = ref ""
                    let comp =
                        resps
                        |> streamingUpdates
                        |> AsyncSeq.map(fun update ->
                            if hasThought then
                                let reasoning = updateReasoning update
                                if Utils.notEmpty reasoning then
                                    thought.Value <- thought.Value + reasoning
                            updateText update)
                        |> AsyncSeq.filter Utils.notEmpty
                        |> AsyncSeq.bufferByCountAndTime 10 1000
                        |> AsyncSeq.filter(fun xs -> xs.Length > 0)
                        |> AsyncSeq.map(String.concat "")
                        |> fun xs -> 
                            if haveCitations then  
                                //----- stream parse citations -----
                                xs
                                |> AsyncSeq.scan StreamParser.updateState (StreamParser.citationsExp cits,(StreamParser.State.Empty,[]))
                                |> AsyncSeq.collect (fun (_,(_,os)) -> os |> List.rev |> AsyncSeq.ofSeq)
                                //-----------------------------
                            else
                                xs 
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
                        if Utils.notEmpty thought.Value then 
                            dispatch (Srv_Ia_Thought(ch.Id,thought.Value))
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
                let! resp = caller.GetResponseAsync(msgs,opts,CancellationToken.None) |> Async.AwaitTask
                let respBody = responseText resp
                let de =
                    {de with
                        Response = respBody
                        OutputTokens = Tokens.tokenSize respBody |> int
                    }
                Monitoring.write (Diag de)
                Srv_Ia_SetSubmissionId(ch.Id,de.id) |> dispatch
                return resp
            with ex ->
                Monitoring.write (Diag {de with Error = ex.Message})
                return raise ex
        }

    let completePrompt parms invCtx ch dispatch modelSelector prompt maxTokens responseFormat =
        async {
            let promptChat =
                ch
                |> Interaction.setUserMessage prompt
                |> fun x -> { x with Parameters = { x.Parameters with MaxTokens = defaultArg maxTokens x.Parameters.MaxTokens } }
            return! completeChat parms invCtx promptChat dispatch modelSelector responseFormat
        }

    let completePromptText parms invCtx ch dispatch modelSelector prompt maxTokens responseFormat =
        async {
            let! resp = completePrompt parms invCtx ch dispatch modelSelector prompt maxTokens responseFormat
            return responseText resp
        }

    let checkStreamCompleteChat (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch modelSelector =
        let mode = Interactions.Interaction.mode ch (Env.allowedModes())
        let haveCitations = mode.IsM_Doc_Index || mode.IsM_Index
        let thoughtOutput = ch.Parameters.Backend.BackendType.IsChatCompletionsHarmony
        if haveCitations || thoughtOutput then 
            streamCompleteChatStructured parms invCtx ch dispatch modelSelector
        else 
            streamCompleteChat parms invCtx ch dispatch modelSelector
