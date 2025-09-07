namespace FsOpenAI.Shared
open System.Text.Json.Serialization
open System
open System.Security.Claims

[<JsonFSharpConverter(SkippableOptionFields=SkippableOptionFields.Always)>]
type ApiEndpoint =
    {
        API_KEY : string
        ENDPOINT : string
        BACKEND  : string
    }

type ModelDeployments = 
    {
        CHAT : string list
        COMPLETION : string list
        EMBEDDING : string list
    }

[<JsonFSharpConverter(SkippableOptionFields=SkippableOptionFields.Always)>]
type ServiceSettings = 
    {
        AZURE_SEARCH_ENDPOINTS: ApiEndpoint list
        CHAT_ENDPOINTS: ApiEndpoint list
        BING_ENDPOINT : ApiEndpoint option
        //GOOGLE_KEY : string option
        OPENAI_KEY : string option
        LOG_CONN_STR : string option
    }
