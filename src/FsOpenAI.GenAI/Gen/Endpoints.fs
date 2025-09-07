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
        let ps = parms.CHAT_ENDPOINTS |> List.filter(fun e -> e.BACKEND = backend.Name)
        if ps.IsEmpty then 
            match backend.Name, parms.OPENAI_KEY with 
            | KnownBackends.OpenAI,Some key -> {API_KEY=key; ENDPOINT="https://api.openai.com/v1/chat/completions"; BACKEND=backend.Name}
            | _ -> raiseNoOpenAIKey()
        else
            randSelect ps

    let private getClientFor (parms:ServiceSettings) backend model : IChatCompletionService =
        let ep = endpoint parms backend
        match backend.Name,backend.BackendType with
        | KnownBackends.AzureOpenAI,_ ->
            Connectors.AzureOpenAI.AzureOpenAIChatCompletionService(model,ep.ENDPOINT,ep.API_KEY)
        | KnownBackends.OpenAI,_  ->            
            OpenAIChatCompletionService(model,Uri ep.ENDPOINT,ep.API_KEY)
        | _,BackendType.ChatCompletions 
        | _,BackendType.ChatCompletionsHarmony -> 
            OpenAIChatCompletionService(model,Uri ep.ENDPOINT,ep.API_KEY)

    let private getEmbeddingsClientFor (parms:ServiceSettings) backend model : ITextEmbeddingGenerationService =
        let ep = endpoint parms backend
        match backend.Name with
        | KnownBackends.AzureOpenAI ->
            Connectors.AzureOpenAI.AzureOpenAITextEmbeddingGenerationService(model,ep.ENDPOINT,ep.API_KEY)
        | KnownBackends.OpenAI ->
            OpenAITextEmbeddingGenerationService(model,ep.API_KEY)
        | x -> 
            failwith $"Unable to generate embeddings for backed {x}"

    let getClient (parms:ServiceSettings) (ch:Interaction) model = getClientFor parms ch.Parameters.Backend model

    let getEmbeddingsClient (parms:ServiceSettings) (ch:Interaction) model = getEmbeddingsClientFor parms ch.Parameters.Backend model
