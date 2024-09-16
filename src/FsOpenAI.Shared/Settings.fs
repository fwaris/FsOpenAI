namespace FsOpenAI.Shared
open System
open System.Security.Claims

type AzureOpenAIEndpoints = 
    {
        API_KEY : string
        RESOURCE_GROUP : string
    }

type ApiEndpoint =
    {
        API_KEY : string
        ENDPOINT : string
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
        AZURE_OPENAI_ENDPOINTS: AzureOpenAIEndpoints list
        EMBEDDING_ENDPOINTS : AzureOpenAIEndpoints list
        BING_ENDPOINT : ApiEndpoint option
        GOOGLE_KEY : string option
        OPENAI_KEY : string option
        LOG_CONN_STR : string option
    }
