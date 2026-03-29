namespace Harmony.Microsoft.Extensions.AI
(* mostly AI generated content *)
open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open FSharp.Control
open Microsoft.Extensions.AI

// Type aliases to disambiguate from FSharp.Control types
type SysAsyncEnumerable<'T> = System.Collections.Generic.IAsyncEnumerable<'T>
type SysAsyncEnumerator<'T> = System.Collections.Generic.IAsyncEnumerator<'T>

/// An IChatClient implementation that parses "harmony" format think tokens from streaming responses.
/// Think tokens in format: <|channel|>analysis<|message|>...<|end|>...actual output...
/// are parsed and returned as TextReasoningContent, while the actual output is returned as TextContent.
type HarmonyChatClient(innerClient: IChatClient) =

    let innerMetadata = innerClient.GetService<ChatClientMetadata>()
    let metadata =
        if innerMetadata <> null then
            ChatClientMetadata("harmony-wrapper", innerMetadata.ProviderUri, innerMetadata.DefaultModelId)
        else
            ChatClientMetadata "harmony-wrapper"

    /// Process streaming updates synchronously from an async enumerable
    let processStreamingUpdates (updates: SysAsyncEnumerable<ChatResponseUpdate>) : SysAsyncEnumerable<ChatResponseUpdate> =
        { new SysAsyncEnumerable<ChatResponseUpdate> with
            member _.GetAsyncEnumerator(ct: CancellationToken) =
                let thought = ref ""
                let parserState = ref (HarmonyGrammar.harmonyExp thought, (StreamParser.State.Empty, []))
                let mutable responseId: string = null
                let mutable messageId: string = null
                let mutable modelId: string = null
                let mutable finishReason = Nullable<ChatFinishReason>()
                let mutable role = Nullable<ChatRole>()
                let mutable innerEnumerator: SysAsyncEnumerator<ChatResponseUpdate> = null
                let mutable pendingOutputs: ChatResponseUpdate list = []
                let mutable thoughtEmitted = false
                let mutable disposed = false
                let mutable innerDone = false

                let moveNextCore () =
                    async {
                        if disposed then
                            return false
                        else
                            // Initialize enumerator on first call
                            if innerEnumerator = null then
                                innerEnumerator <- updates.GetAsyncEnumerator ct

                            // If we have pending outputs, consume the head
                            if not pendingOutputs.IsEmpty then
                                pendingOutputs <- pendingOutputs.Tail

                            // Get more from inner enumerator when no buffered outputs remain
                            while pendingOutputs.IsEmpty && not innerDone do
                                let! hasMore =
                                    innerEnumerator.MoveNextAsync().AsTask()
                                    |> Async.AwaitTask
                                if not hasMore then
                                    innerDone <- true
                                else
                                    let update = innerEnumerator.Current

                                    // Capture metadata from updates
                                    if responseId = null && update.ResponseId <> null then
                                        responseId <- update.ResponseId
                                    if messageId = null && update.MessageId <> null then
                                        messageId <- update.MessageId
                                    if modelId = null && update.ModelId <> null then
                                        modelId <- update.ModelId
                                    if update.FinishReason.HasValue then
                                        finishReason <- update.FinishReason
                                    if not role.HasValue && update.Role.HasValue then
                                        role <- update.Role

                                    // Extract text content from the update
                                    let textContent =
                                        update.Contents
                                        |> Seq.choose (fun c ->
                                            match c with
                                            | :? TextContent as tc -> Some tc.Text
                                            | _ -> None)
                                        |> String.concat ""

                                    if not (String.IsNullOrEmpty textContent) then
                                        // Run the chunk through the parser
                                        let newState = StreamParser.updateState !parserState textContent
                                        parserState := newState

                                        let _, (_, outputs) = newState

                                        // Create output updates for parsed chunks
                                        let newUpdates =
                                            outputs
                                            |> List.rev
                                            |> List.filter (not << String.IsNullOrEmpty)
                                            |> List.map (fun output ->
                                                let newUpdate = ChatResponseUpdate()
                                                newUpdate.ResponseId <- responseId
                                                newUpdate.MessageId <- messageId
                                                newUpdate.ModelId <- modelId
                                                newUpdate.Role <- role
                                                newUpdate.Contents.Add(TextContent output)
                                                newUpdate)

                                        pendingOutputs <- newUpdates

                            // If inner is done and we haven't emitted thought yet
                            if innerDone && pendingOutputs.IsEmpty && not thoughtEmitted then
                                if not (String.IsNullOrEmpty thought.Value) then
                                    thoughtEmitted <- true
                                    let thoughtUpdate = ChatResponseUpdate()
                                    thoughtUpdate.ResponseId <- responseId
                                    thoughtUpdate.MessageId <- messageId
                                    thoughtUpdate.ModelId <- modelId
                                    thoughtUpdate.Role <- role
                                    thoughtUpdate.FinishReason <- finishReason
                                    thoughtUpdate.Contents.Add(TextReasoningContent thought.Value)
                                    pendingOutputs <- [thoughtUpdate]

                            return not pendingOutputs.IsEmpty
                    }
                    |> Async.StartAsTask

                { new SysAsyncEnumerator<ChatResponseUpdate> with
                    member _.Current =
                        match pendingOutputs with
                        | h :: _ -> h
                        | [] -> Unchecked.defaultof<ChatResponseUpdate>

                    member _.MoveNextAsync() =
                        ValueTask<bool>(moveNextCore())

                    member _.DisposeAsync() =
                        let disposeTask =
                            async {
                                if not disposed then
                                    disposed <- true
                                    if innerEnumerator <> null then
                                        do! innerEnumerator.DisposeAsync().AsTask()
                                            |> Async.AwaitTask
                            }
                            |> Async.StartAsTask

                        ValueTask(disposeTask)
                }
        }

    interface IChatClient with

        member _.GetService(serviceType: Type, [<Optional; DefaultParameterValue(null:obj)>] serviceKey: obj) : obj =
            if serviceType = typeof<ChatClientMetadata> then
                box metadata
            elif serviceType = typeof<IChatClient> then
                box innerClient
            else
                innerClient.GetService(serviceType, serviceKey)

        member this.GetResponseAsync(messages: Collections.Generic.IEnumerable<ChatMessage>, [<Optional; DefaultParameterValue(null:ChatOptions)>] options: ChatOptions, [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken) : Task<ChatResponse> =
            let workflow =
                async {
                    let contents = ResizeArray<AIContent>()
                    let! innerResponse =
                        innerClient.GetResponseAsync(messages, options, cancellationToken)
                        |> Async.AwaitTask

                    let textBuilder = Text.StringBuilder()
                    let reasoningBuilder = Text.StringBuilder()

                    let mutable role = ChatRole.Assistant

                    for message in innerResponse.Messages do
                        role <- message.Role
                        for content in message.Contents do
                            match content with
                            | :? TextContent as tc -> textBuilder.Append tc.Text |> ignore
                            | :? TextReasoningContent as rc -> reasoningBuilder.Append rc.Text |> ignore
                            | other -> contents.Add other

                    let output, harmonyReasoning =
                        HarmonyGrammar.splitHarmony (textBuilder.ToString())

                    let reasoning =
                        [ reasoningBuilder.ToString(); harmonyReasoning |> Option.defaultValue "" ]
                        |> List.filter (String.IsNullOrWhiteSpace >> not)
                        |> String.concat "\n\n"

                    if reasoningBuilder.Length > 0 then
                        contents.Insert(0, TextReasoningContent(reasoning))
                    elif not (String.IsNullOrWhiteSpace reasoning) then
                        contents.Insert(0, TextReasoningContent(reasoning))
                    if not (String.IsNullOrWhiteSpace output) then
                        contents.Add(TextContent(output))

                    let message = ChatMessage(role, contents)
                    let normalizedResponse = ChatResponse message
                    normalizedResponse.ResponseId <- innerResponse.ResponseId
                    normalizedResponse.ModelId <- innerResponse.ModelId
                    normalizedResponse.FinishReason <- innerResponse.FinishReason
                    return normalizedResponse
                }

            Async.StartAsTask(workflow, cancellationToken = cancellationToken)

        member _.GetStreamingResponseAsync(messages: System.Collections.Generic.IEnumerable<ChatMessage>, [<Optional; DefaultParameterValue(null:ChatOptions)>] options: ChatOptions, [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken) : SysAsyncEnumerable<ChatResponseUpdate> =
            let innerStream = innerClient.GetStreamingResponseAsync(messages, options, cancellationToken)
            processStreamingUpdates innerStream

    interface IDisposable with
        member _.Dispose() =
            innerClient.Dispose()


/// Extension methods for creating HarmonyChatClient
[<Extension>]
type HarmonyChatClientExtensions =

    /// Wraps an IChatClient to parse harmony format think tokens.
    /// Think tokens in format: <|channel|>analysis<|message|>...<|end|>...actual output...
    /// are parsed and returned as TextReasoningContent, while the actual output is returned as TextContent.
    [<Extension>]
    static member AsHarmonyClient(client: IChatClient) =
        new HarmonyChatClient(client) :> IChatClient
