module FsOpenAI.GenAI.Endpoints
open System
open System.Text.Json.Serialization
open FsOpenAI.Shared
open Microsoft.Extensions.AI
open Harmony.Microsoft.Extensions.AI
open System.ClientModel

[<RequireQualifiedAccess>]
module Endpoints =
    let rng = Random()
    let randSelect (ls:_ list) = ls.[rng.Next(ls.Length)]

    let raiseNoOpenAIKey() = raise (ConfigurationError "No OpenAI key configured. Please set the key in application settings")

    let endpoint (parms:ServiceSettings) (backend:Backend) : ApiEndpoint =
        let ps = parms.CHAT_ENDPOINTS |> List.filter(fun e -> e.BACKEND = Include backend.Name)
        if ps.IsEmpty then
            match backend.Name, parms.OPENAI_KEY with
            | KnownBackends.OpenAI, Include key ->
                { API_KEY = key; ENDPOINT = "https://api.openai.com/v1/chat/completions"; BACKEND = Include backend.Name }
            | _ -> raiseNoOpenAIKey()
        else
            randSelect ps

    let private chatApiBase backend endpoint =
        if String.IsNullOrWhiteSpace endpoint then
            None
        else
            let trimmed = endpoint.TrimEnd('/')
            let suffix = "/chat/completions"
            if backend.Name = KnownBackends.OpenAI && trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) then
                let baseUri = trimmed.Substring(0, trimmed.Length - suffix.Length)
                Some baseUri
            else
                Some trimmed

    let private createOpenAIClient backend ep =
        match chatApiBase backend ep.ENDPOINT with
        | Some endpointUri ->
            let options = OpenAI.OpenAIClientOptions()
            options.Endpoint <- Uri endpointUri
            new OpenAI.OpenAIClient(new ApiKeyCredential(ep.API_KEY), options)
        | None ->
            new OpenAI.OpenAIClient(ep.API_KEY)

    let private createChatClient backend ep model : IChatClient =
        let client = createOpenAIClient backend ep
        let chatClient = client.GetChatClient(model).AsIChatClient()
        if backend.BackendType.IsChatCompletionsHarmony then
            chatClient.AsHarmonyClient()
        else
            chatClient

    let private getClientFor (parms:ServiceSettings) backend model : IChatClient =
        let ep = endpoint parms backend
        createChatClient backend ep model

    let private getEmbeddingsClientFor (parms:ServiceSettings) backend model : IEmbeddingGenerator<string, Embedding<float32>> =
        let ep = endpoint parms backend
        let client = createOpenAIClient backend ep
        client.GetEmbeddingClient(model).AsIEmbeddingGenerator()

    let getClient (parms:ServiceSettings) (ch:Interaction) model = getClientFor parms ch.Parameters.Backend model

    let getEmbeddingsClient (parms:ServiceSettings) (ch:Interaction) model = getEmbeddingsClientFor parms ch.Parameters.Backend model
