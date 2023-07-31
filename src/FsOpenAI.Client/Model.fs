namespace FsOpenAI.Client 
open System
open Elmish

type AzureOpenAIEndpoints = 
    {
        API_KEY : string
        RESOURCE_GROUP : string
        API_VERSION : string
    }

type AzureSearchEndpoints =
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
        OPENAI_KEY : string option
        AZURE_SEARCH_ENDPOINTS: AzureSearchEndpoints list
        AZURE_OPENAI_ENDPOINTS: AzureOpenAIEndpoints list
        AZURE_OPENAI_MODELS : ModelDeployments option
        OPENAI_MODELS : ModelDeployments option
    }

type UserRoleStatus = Open | Closed
type MessageRole = User of UserRoleStatus | Assistant

type InteractionMessage = {Role:MessageRole; Message: string} 
    with 
        member this.IsOpen = 
            match this.Role with 
            | MessageRole.User Open -> true 
            | _ -> false

        member this.IsUser = 
            match this.Role with 
            | MessageRole.User _ -> true 
            | _ -> false

type ServiceModel = {Model:string; ApiVersion:string} 
type Backend = OpenAI | AzureOpenAI

type InteractionParameters = 
    {
        Backend    : Backend
        Temperature : float
        PresencePenalty : float
        FrequencyPenalty : float
        MaxTokens : int 
        ChatModel : string
        CompletionsModel : string
        EmbeddingsModel : string       
    }

    static member Default = 
        {
            Backend = AzureOpenAI
            Temperature = 1.0      //0.0 to 2.0
            PresencePenalty = 0.0  //-2.0 to +2.0
            FrequencyPenalty = 0.0 //-2.0 to +2.0
            MaxTokens = 1000            
            ChatModel = "gpt-4"
            CompletionsModel = "text-davinci-003"
            EmbeddingsModel = "text-embedding-ada-002"
        }

type IndexRef =
    | Azure of {| Name : string; |}

type Document = {Text:string; Embedding:float32[]; Ref:string}
    
type QABag =
    {
        Index : IndexRef option
        MaxDocs : int                                        
        Documents : Document list
    }
    with static member Default =
            {
                Index = None
                MaxDocs = 10
                Documents = []
            }

type InteractionType =
    | Chat of string
    | QA of QABag

type Interaction = { 
    Id : string
    Name: string
    InteractionType: InteractionType    
    Messages : InteractionMessage list
    Parameters : InteractionParameters
    Timestamp : DateTime  
    IsBuffering : bool
    Notification : string option
}

type InteractionCreateType =
    | CreateChat of Backend
    | CreateQA of Backend

type Model =
    {
        interactions : Interaction list 
        interactionCreateTypes : (string * InteractionCreateType) list
        indexRefs : IndexRef list
        error : string option
        busy : bool
        settingsOpen : Map<string,bool>
        highlight_busy : bool
        serviceParameters : ServiceSettings option
    }

type ServerInitiatedMessages =
    | Srv_Parameters of ServiceSettings
    | Srv_Ia_Delta of string*int*string   //chat id,index,delta
    | Srv_Ia_Done of string*string option //chat id (optional error)
    | Srv_Ia_Notification of string*string option //chat id (optional error)
    | Srv_Error of string
    | Srv_IndexesRefreshed of IndexRef list * string option * bool

type ClientInitiatedMessages =
    | Clnt_Connected of string
    | Clnt_StreamChat of ServiceSettings*Interaction
    | Clnt_RefreshIndexes of ServiceSettings * bool
    | Clnt_StreamAnswer of ServiceSettings*Interaction

type Message =
    | Chat_SysPrompt of string * string
    | Ia_AddMsg of string * InteractionMessage    
    | Ia_UpdateLastMsg of string * string    
    | Ia_AddDelta of string * string
    | Ia_Completed of string * string option //id and optional error
    | Ia_DeleteMsg of string * InteractionMessage
    | Ia_UpdateName of string * string
    | Ia_UpdateParms of string * InteractionParameters
    | Ia_Add of InteractionCreateType
    | Ia_Remove of string 
    | Ia_UpdateQaBag of string * QABag
    | Ia_Notification of string * string option
    | SubmitInteraction of string*string
    | RefreshIndexes of bool
    | IndexesRefreshed of IndexRef list * string option //indexes or error
    | Nop of unit
    | Clear 
    | Error of exn
    | ShowError of string
    | ShowInfo of string
    | ClearError
    | Reset 
    | OpenCloseSettings of string
    | HighlightBusy of bool
    | SetServiceParms of ServiceSettings 
    | Started
    | FromServer of ServerInitiatedMessages
    | AddSamples