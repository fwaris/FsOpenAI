namespace FsOpenAI.Client
open System
open FSharp.Control
open FsOpenAI.Client

module Completions =
    type ApiMsg = Azure.AI.OpenAI.ChatMessage // OpenAI_API.Chat.ChatMessage
    type ApiRole = Azure.AI.OpenAI.ChatRole // OpenAI_API.Chat.ChatMessageRole

    let getAzureEndpoint (parms:ServiceSettings) =
        if parms.AZURE_OPENAI_ENDPOINTS.IsEmpty then failwith "No Azure OpenAI endpoints configured"
        let endpt = Utils.randSelect parms.AZURE_OPENAI_ENDPOINTS
        let url = $"https://{endpt.RESOURCE_GROUP}.openai.azure.com"
        url,endpt.API_KEY

    let getClient (parms:ServiceSettings) (ch:Interaction) =
            match ch.Parameters.Backend with 
            | AzureOpenAI -> 
                let url,key = getAzureEndpoint parms
                let clr = Azure.AI.OpenAI.OpenAIClient(Uri url,Azure.AzureKeyCredential(key))                        
                clr
            | OpenAI  ->               
                Azure.AI.OpenAI.OpenAIClient(parms.OPENAI_KEY.Value)                        

    let completeChat (parms:ServiceSettings) (ch:Interaction) dispatch = 
        //postDelta postEnd postErr (ch:Interaction) =
       async {
            let sysMsg = match ch.InteractionType with Chat s -> s | _ -> ""
            let messages = 
                seq {
                    if String.IsNullOrWhiteSpace sysMsg |> not then
                        yield ApiMsg(ApiRole.System, sysMsg)
                    for m in ch.Messages do
                        let role = 
                            match m.Role with 
                            | User _-> ApiRole.User
                            | Assistant  _ -> ApiRole.Assistant
                        if String.IsNullOrWhiteSpace m.Message |> not then 
                            yield ApiMsg(role,m.Message)                                 
                }
            let caller =  getClient parms ch
            let opts = Azure.AI.OpenAI.ChatCompletionsOptions(messages)
            opts.Temperature <- float32 ch.Parameters.Temperature
            opts.PresencePenalty <- float32 ch.Parameters.PresencePenalty
            opts.FrequencyPenalty <- float32 ch.Parameters.FrequencyPenalty
            opts.MaxTokens <- ch.Parameters.MaxTokens

            try 
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

