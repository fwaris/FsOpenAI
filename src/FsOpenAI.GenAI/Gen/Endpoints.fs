module FsOpenAI.GenAI.Endpoints
open System
open Microsoft.SemanticKernel
open FsOpenAI.Shared
open Microsoft.SemanticKernel.Connectors.OpenAI
open Microsoft.SemanticKernel.ChatCompletion
open Microsoft.SemanticKernel.Embeddings
    
[<RequireQualifiedAccess>]
module Endpoints =
    let rng = Random()
    let randSelect (ls:_ list) = ls.[rng.Next(ls.Length)]

    let raiseNoOpenAIKey() = raise (ConfigurationError "No OpenAI key configured. Please set the key in application settings")

    let endpoint (parms:ServiceSettings) (backend:Backend) : ApiEndpoint =
        let ps = parms.CHAT_ENDPOINTS |> List.filter(fun e -> e.BACKEND = Some backend)
        if ps.IsEmpty then 
            match backend, parms.OPENAI_KEY with 
            | OpenAI,Some key -> {API_KEY=key; ENDPOINT="https://api.openai.com/v1/chat/completions"; BACKEND=Some backend}
            | _ -> raiseNoOpenAIKey()
        else
            randSelect ps

    let private getClientFor (parms:ServiceSettings) backend model : IChatCompletionService =
        let ep = endpoint parms backend
        match backend with
        | AzureOpenAI ->
            Connectors.AzureOpenAI.AzureOpenAIChatCompletionService(model,ep.ENDPOINT,ep.API_KEY)
        | OpenAI  ->            
            OpenAIChatCompletionService(model,Uri ep.ENDPOINT,ep.API_KEY)

    let private getEmbeddingsClientFor (parms:ServiceSettings) backend model : ITextEmbeddingGenerationService =
        let ep = endpoint parms backend
        match backend with
        | AzureOpenAI ->
            Connectors.AzureOpenAI.AzureOpenAITextEmbeddingGenerationService(model,ep.ENDPOINT,ep.API_KEY)

        | OpenAI  ->
            OpenAITextEmbeddingGenerationService(model,ep.API_KEY)

    let getClient (parms:ServiceSettings) (ch:Interaction) model = getClientFor parms ch.Parameters.Backend model

    let getEmbeddingsClient (parms:ServiceSettings) (ch:Interaction) model = getEmbeddingsClientFor parms ch.Parameters.Backend model
