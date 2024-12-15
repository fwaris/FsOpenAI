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

    let getAzureEndpoint (endpoints:AzureOpenAIEndpoints list) =
        if List.isEmpty endpoints then raise (ConfigurationError "No Azure OpenAI endpoints configured")
        let endpt = randSelect endpoints
        let rg = endpt.RESOURCE_GROUP
        let url = $"https://{rg}.openai.azure.com"
        rg,url,endpt.API_KEY
 
    let raiseNoOpenAIKey() = raise (ConfigurationError "No OpenAI key configured. Please set the key in application settings")
    
    let getOpenAIEndpoint parms =
        match parms.OPENAI_KEY with
        | Some key when Utils.notEmpty key -> "https://api.openai.com/v1/chat/completions",key
        | _ -> raiseNoOpenAIKey()

    let serviceEndpoint (parms:ServiceSettings) (backend:Backend) (model:string) =
        match backend with
        | AzureOpenAI ->
            let rg,url,key = getAzureEndpoint parms.AZURE_OPENAI_ENDPOINTS
            let url = $"https://{rg}.openai.azure.com/openai/deployments/{model}/chat/completions?api-version=2023-07-01-preview";
            url,key
        | OpenAI -> getOpenAIEndpoint parms

    let private getClientFor (parms:ServiceSettings) backend model : IChatCompletionService*string =
            match backend with
            | AzureOpenAI ->
                let rg,url,key = getAzureEndpoint parms.AZURE_OPENAI_ENDPOINTS
                let clr = Connectors.AzureOpenAI.AzureOpenAIChatCompletionService(model,url,key)
                clr,rg
            | OpenAI  ->
                let key = match parms.OPENAI_KEY with Some key when Utils.notEmpty key -> key | _ -> raiseNoOpenAIKey()
                OpenAIChatCompletionService(model,key),"OpenAI"

    let private getEmbeddingsClientFor (parms:ServiceSettings) backend model : ITextEmbeddingGenerationService*string =
            match backend with
            | AzureOpenAI ->
                let rg,url,key = getAzureEndpoint parms.AZURE_OPENAI_ENDPOINTS
                let clr = Connectors.AzureOpenAI.AzureOpenAITextEmbeddingGenerationService(model,url,key)
                clr,rg
            | OpenAI  ->
                let key = match parms.OPENAI_KEY with Some key when Utils.notEmpty key -> key | _ -> raiseNoOpenAIKey()
                OpenAITextEmbeddingGenerationService(model,key),"OpenAI"

    let getClient (parms:ServiceSettings) (ch:Interaction) model = getClientFor parms ch.Parameters.Backend model

    let getEmbeddingsClient (parms:ServiceSettings) (ch:Interaction) model = getEmbeddingsClientFor parms ch.Parameters.Backend model
