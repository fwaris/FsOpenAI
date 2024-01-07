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
    }

type Document = {Text:string; Embedding:float32[]; Ref:string; Title:string}

type QueriedDocuments = {SearchQuery:string option; Docs: Document list } 
    with static member Empty = {SearchQuery=None; Docs=[]}
    
type MessageRole = User | Assistant of QueriedDocuments

type InteractionMessage = {MsgId:string; Role:MessageRole; Message: string} 
    with 
        member this.IsUser = 
            match this.Role with 
            | MessageRole.User  -> true 
            | _                 -> false

type ExplorationMode = Factual | Exploratory | Creative
type InteractionParameters = 
    {
        Backend             : Backend
        Mode                : ExplorationMode
        MaxTokens           : int 
    }

    static member Default = 
        {
            Backend = AzureOpenAI
            Mode = Factual
            MaxTokens = 1000            
        }

type IndexRef =
    | Azure of name:string
    | Virtual of name:string
    //other indexes e.g. pinecone can be added
    with 
        member this.Name = 
            match this with 
            | Azure n -> n
            | Virtual n -> n
        member this.isVirtual = 
            match this with 
            | Azure _ -> false
            | Virtual _ -> true

type IndexTree = {Idx:IndexRef; Description:string; Children: IndexTree list}
    with member this.isVirtual = this.Idx.isVirtual
    
type QABag =
    {
        Indexes         : IndexRef list
        MaxDocs         : int    
        HybridSearch    : bool
    }
    with static member Default =
            {
                Indexes = []
                MaxDocs = 10
                HybridSearch = true    
            }

type DocumentStatus = No_Document | Uploading | Receiving | ExtractingTerms | Ready

type DocumentContent =
    {
        DocumentRef : IBrowserFile option
        DocumentText : string option
        Status : DocumentStatus
    }
    with 
        static member Default = {DocumentRef=None; DocumentText=None;Status=No_Document}


type DocBag =
    {
        QABag : QABag
        Label : string
        Document : DocumentContent
        SearchTerms : string option
        ExtractTermsTemplate: string option
        SearchWithOrigText: bool            //use raw document text instead of extracted search terms for index search
        QueryTemplate : string option
        DocOnlyQuery : bool                 //Q&A with document content only. Ignore indexes
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
                DocOnlyQuery = false
            }

type ChatBag =
    {
        UseWeb : bool
    }
    with static member Default = {UseWeb=false;}

type InteractionType =
    | Chat of ChatBag
    | QA of QABag
    | DocQA of DocBag

type Interaction = { 
    Id : string
    Name: string option
    Question : string 
    SystemMessage : string
    InteractionType: InteractionType    
    Messages : InteractionMessage list
    Parameters : InteractionParameters
    Timestamp : DateTime  
    IsBuffering : bool
    Notifications : string list
}

type InteractionCreateType =
    | CreateChat
    | CreateQA
    | CreateDocQA of string //template 

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
        SampleSysMsg     : string
        SampleQuestion   : string
        PreferredModels  : string list
        SampleMode       : ExplorationMode
        MaxDocs          : int
    }

type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/authentication/{action}">] Authentication of action:string //need a separate route for authentication

type AuthUser = {Name:string; IsAuthorized:bool; Principal:ClaimsPrincipal; Roles:Set<string>}
type UserState = Authenticated of AuthUser | Unauthenticated

type TempChatState = 
    {
        SettingsOpen: bool
        DocsOpen:string option
        DocDetailsOpen:bool
        PromptsOpen:bool
        IndexOpen:bool
        SysMsgOpen:bool
    }
    with 
        static member Default = 
            {
                SettingsOpen=false
                DocsOpen=None
                DocDetailsOpen=false
                PromptsOpen=false
                IndexOpen=false
                SysMsgOpen=false
            }

type Model =
    {
        flashBanner             : bool   
        page                    : Page
        selectedChatId          : string option
        interactions            : Interaction list 
        appConfig               : AppConfig
        templates               : LabeledTemplates list
        indexTrees              : IndexTree list
        error                   : string option
        busy                    : bool
        tempChatSettings        : Map<string,TempChatState>
        settingsOpen            : Map<string,bool>
        serviceParameters       : ServiceSettings option
        darkTheme               : bool
        theme                   : MudBlazor.MudTheme
        tabsUp                  : bool
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
    | Srv_IndexesRefreshed of IndexTree list
    | Srv_Ia_SetContents of string*string*bool
    | Srv_SetTemplates of LabeledTemplates list
    | Srv_LoadSamples of (string*SamplePrompt list)
    | Srv_SetConfig of AppConfig
    | Srv_DoneInit of unit

type ClientInitiatedMessages =
    | Clnt_Connected of string
    | Clnt_RefreshIndexes of ServiceSettings * bool * string list * string // settings * is initial request * template names * metaIndex
    | Clnt_ProcessChat of ServiceSettings*ModelsConfig*Interaction
    | Clnt_ProcessQA of ServiceSettings*ModelsConfig*Interaction
    | Clnt_ProcessDocQA of ServiceSettings*ModelsConfig*Interaction
    | Clnt_UploadChunk of string*byte[]
    | Clnt_ExtractContents of string*string
    | Clnt_SearchQuery of ServiceSettings*ModelsConfig*Interaction

type Message =
    | StartInit
    | Ia_SystemMessage of string * string
    | Ia_ApplyTemplate of string*TemplateType*Template
    | Ia_SetPrompt of string*TemplateType*string
    | Ia_Save
    | Ia_LoadChats
    | Ia_LoadedChats of Interaction list
    | Ia_ClearChats 
    | Ia_ClearChat of string * string
    | Ia_ToggleDocOnly of string
    | SaveUIState
    | LoadUIState
    | LoadedUIState of bool*bool //darkTheme,tabsUp
    | Ia_DeleteSavedChats
    | Ia_AddMsg of string * InteractionMessage    
    | Ia_SetQuestion of string * string    
    | Ia_AddDelta of string * string
    | Ia_Completed of string * string option //id and optional error
    | Ia_Restart of string * InteractionMessage
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
    | Ia_UseWeb of string*bool
    | Ia_Submit of string*string
    | Ia_SubmitOnKey of string*bool
    | Ia_ToggleSettings of string
    | Ia_ToggleSysMsg of string
    | Ia_ToggleDocs of string*string option
    | Ia_ToggleDocDetails of string
    | Ia_TogglePrompts of string
    | Ia_OpenIndex of string
    | Ia_SetIndex of string*IndexRef list   
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
    | PurgeLocalData
    | SaveToLocal of string*obj
    | IgnoreError of exn
    | ToggleTheme
    | ToggleTabs
    | SetPage of Page
    | SetAuth of ClaimsPrincipal option
    | LoginLogout
    | GetUserDetails 
    | GotUserDetails of string option
