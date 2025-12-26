namespace FsOpenAI.Shared
open System.Text.Json.Serialization
open System
open System.Security.Claims

type ApiEndpoint =
    {
        API_KEY : string
        ENDPOINT : string
        BACKEND  : Skippable<string>
    }

type ModelDeployments = 
    {
        CHAT : string list
        COMPLETION : string list
        EMBEDDING : string list
    }

type ServiceSettings = 
    {
        AZURE_SEARCH_ENDPOINTS: ApiEndpoint list
        CHAT_ENDPOINTS: ApiEndpoint list
        BING_ENDPOINT : Skippable<ApiEndpoint>
        OPENAI_KEY : Skippable<string>
        LOG_CONN_STR : Skippable<string>
    }
