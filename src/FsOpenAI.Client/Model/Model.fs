namespace FsOpenAI.Client 
open System
open System.Security.Claims
open Microsoft.AspNetCore.Components.Forms
open Bolero

type AzureOpenAIEndpoints = 
    {
        API_KEY : string
        RESOURCE_GROUP : string
        API_VERSION : string
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
        OPENAI_KEY : string option
        BING_ENDPOINT : ApiEndpoint option
        AZURE_SEARCH_ENDPOINTS: ApiEndpoint list
        AZURE_OPENAI_ENDPOINTS: AzureOpenAIEndpoints list
        AZURE_OPENAI_MODELS : ModelDeployments option
        OPENAI_MODELS : ModelDeployments option
    }

type Document = {Text:string; Embedding:float32[]; Ref:string; Title:string}
type QueriedDocuments = {SearchQuery:string option; Docs: Document list } with static member Empty = {SearchQuery=None; Docs=[]}
type UserRoleStatus = Open | Closed
type MessageRole = User of UserRoleStatus | Assistant of QueriedDocuments

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
            Temperature = 0.1      // 0.0 to 2.0
            PresencePenalty = 0.0  // -2.0 to +2.0
            FrequencyPenalty = 0.0 // -2.0 to +2.0
            MaxTokens = 1000            
            ChatModel = "gpt-4"
            CompletionsModel = "text-davinci-003"
            EmbeddingsModel = "text-embedding-ada-002"
        }

type VectorIndex = {Name:string; Description:string}
type IndexRef =
    | Azure of VectorIndex
    //other indexes e.g. pinecone can be added

    
type QABag =
    {
        SystemMessage   : string
        Indexes         : IndexRef list
        MaxDocs         : int                                        
    }
    with static member Default =
            {
                SystemMessage = C.defaultSystemMessage
                Indexes = []
                MaxDocs = 10
            }

type DocumentStatus = No_Document | Uploading | Extracting | GenSearch | Ready

type DocumentContent =
    {
        DocumentRef : IBrowserFile option
        DocumentText : string option
        Status : DocumentStatus
    }
    with static member Default = {DocumentRef=None; DocumentText=None;Status=No_Document}

type DocBag =
    {
        QABag : QABag
        Label : string
        Document : DocumentContent
        SearchTerms : string option
        ExtractTermsTemplate: string option
        SearchWithOrigText: bool            //use raw document text instead of extracted search terms for index search
        QueryTemplate : string option
    }
    with static member Default =
            {
                Label = "Default"
                QABag = QABag.Default
                Document = DocumentContent.Default
                SearchTerms = None
                ExtractTermsTemplate = None
                SearchWithOrigText = false  
                QueryTemplate = None
            }

type ChatBag =
    {
        SystemMessage: string
        UseWeb : bool
    }
    with static member Default = {SystemMessage=C.defaultSystemMessage; UseWeb=false;}

type InteractionType =
    | Chat of ChatBag
    | QA of QABag
    | DocQA of DocBag

type Interaction = { 
    Id : string
    Name: string option
    InteractionType: InteractionType    
    Messages : InteractionMessage list
    Parameters : InteractionParameters
    Timestamp : DateTime  
    IsBuffering : bool
    Notifications : string list
}

type InteractionCreateType =
    | CreateChat of Backend
    | CreateQA of Backend
    | CreateDocQA of Backend * string

type Template =
    {
        Name : string
        Description : string
        Template : string
        Question : string option
    }

type TemplateType = DocQuery | Extraction

type LabeledTemplates =
    {
        Label : string
        Templates : Map<TemplateType,Template list>
    }

type SampleChatType =
    | Simple_Chat of bool
    | QA_Chat of string
    | DocQA_Chat of string 

type SamplePrompt =
    {
        SampleChatType   : SampleChatType
        SystemMessage    : string
        SampleQuestion   : string
        PreferredModels  : string list
        Temperature      : float
        MaxDocs          : int
    }

type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/authentication/{action}">] Authentication of action:string //need a separate route for authentication

type AuthUser = {Name:string; IsAuthorized:bool; Principal:ClaimsPrincipal; Roles:Set<string>}
type UserState = Authenticated of AuthUser | Unauthenticated

type Model =
    {
        page                    : Page
        selected                : string option
        interactions            : Interaction list 
        appConfig               : AppConfig
        templates               : LabeledTemplates list
        interactionCreateTypes  : (string*string* InteractionCreateType) list
        indexRefs               : IndexRef list
        error                   : string option
        busy                    : bool
        settingsOpen            : Map<string,bool>
        serviceParameters       : ServiceSettings option
        darkTheme               : bool
        theme                   : MudBlazor.MudTheme
        user                    : UserState
        photo                   : string option
    }

type ServerInitiatedMessages =
    | Srv_Parameters of ServiceSettings
    | Srv_Ia_Delta of string*int*string   //chat id,index,delta
    | Srv_Ia_SetSearch of string*string
    | Srv_Ia_SetDocs of string*Document list 
    | Srv_Ia_Done of string*string option //chat id (optional error)
    | Srv_Ia_Notification of string*string //chat id (optional error)
    | Srv_Error of string
    | Srv_Info of string    
    | Srv_IndexesRefreshed of IndexRef list
    | Srv_Ia_SetContents of string*string*bool
    | Srv_SetTemplates of LabeledTemplates list
    | Srv_LoadSamples of (string*SamplePrompt list)
    | Srv_SetConfig of AppConfig
    | Srv_DoneInit of unit
    

type ClientInitiatedMessages =
    | Clnt_Connected of string
    | Clnt_RefreshIndexes of ServiceSettings * bool * string list // settings * is initial request * template names
    | Clnt_ProcessChat of ServiceSettings*Interaction
    | Clnt_ProcessQA of ServiceSettings*Interaction
    | Clnt_ProcessDocQA of ServiceSettings*Interaction
    | Clnt_UploadChunk of string*byte[]
    | Clnt_ExtractContents of string*string
    | Clnt_SearchQuery of ServiceSettings*Interaction

type Message =
    | Init
    | Ia_SystemMessage of string * string
    | Ia_ApplyTemplate of string*TemplateType*Template
    | Ia_SetPrompt of string*TemplateType*string
    | Ia_Save
    | Ia_LoadChats
    | Ia_LoadedChats of Interaction list
    | Ia_ClearChats 
    | Ia_DeleteSavedChats
    | Ia_AddMsg of string * InteractionMessage    
    | Ia_UpdateLastMsg of string * string    
    | Ia_AddDelta of string * string
    | Ia_Completed of string * string option //id and optional error
    | Ia_DeleteMsg of string * InteractionMessage
    | Ia_UpdateName of string * string
    | Ia_UpdateParms of string * InteractionParameters
    | Ia_Add of InteractionCreateType
    | Ia_Remove of string 
    | Ia_Selected of string
    | Ia_UpdateQaBag of string * QABag
    | Ia_UpdateDocBag of string * DocBag
    | Ia_Notification of string * string
    | Ia_File_BeingLoad of string * DocBag
    | Ia_File_Load of string
    | Ia_File_Loaded of string*string
    | Ia_File_SetContents of string*string*bool
    | Ia_SetSearch of string*string
    | Ia_GenSearch of string
    | Ia_Submit of string*string
    | Ia_UseWeb of string*bool
    | RefreshIndexes of bool
    | Nop of unit
    | Clear 
    | Error of exn
    | ShowError of string
    | ShowInfo of string
    | FlashInfo of string
    | ClearError
    | Reset 
    | OpenCloseSettings of string
    | FromServer of ServerInitiatedMessages
    | GetOpenAIKey
    | SetOpenAIKey of string
    | UpdateOpenKey of string
    | SaveToLocal of string*string
    | IgnoreError of exn
    | ToggleTheme
    | SetPage of Page
    | SetAuth of ClaimsPrincipal option
    | LoginLogout
    | GetUserDetails 
    | GotUserDetails of string option
