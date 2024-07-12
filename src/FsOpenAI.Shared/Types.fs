namespace FsOpenAI.Shared
open System
open System.Security.Claims

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

type DocType = DT_Pdf | DT_Word | DT_Powerpoint | DT_Excel | DT_Text | DT_RTF

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

type IndexTree = {Idx:IndexRef; IndexName:string option; Description:string; Tag:string; Children: IndexTree list}
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

type CodeEvalParms =
    {
        ///Code that will be evaluated into fsi before evaluation of the generated code
        ///(can refer to dlls; 'open' namespaces; etc.)
        Preamble : string

        ///SK format prompt template that takes 'code' and 'errorMessage' string variables
        ///and returns a code regeneration prompt (as a user message)
        RegenPrompt : string

        ///System prompt for code regen
        RegenSystemPrompt : string option
    }
    with static member Default =
            {
                Preamble = ""
                RegenPrompt = "{{$code}}\n\n{{$errorMessage}}"
                RegenSystemPrompt = None
            }

type DocumentContent =
    {
        //IBrowserFile option (keeping untyped to reduce dependency into asp.net core)
        DocumentRef : obj option
        DocType : DocType option
        DocumentText : string option
        Status : DocumentStatus
    }
    with
        static member Default =
                        {
                            DocumentRef = None
                            DocumentText = None
                            Status = No_Document
                            DocType = None
                        }

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


type CodeEvalBag =
    {
        Code : string option
        Plan : string option
        CodeEvalParms : CodeEvalParms
    }
    with static member Default = {Code=None; Plan=None; CodeEvalParms=CodeEvalParms.Default}

///type of chat - each type may have its own data
type InteractionType =
    | Plain of ChatBag
    | IndexQnA of QABag
    | IndexQnADoc of DocBag
    | QnADoc of DocumentContent
    | CodeEval of CodeEvalBag

///Create new chat enum
type InteractionCreateType =
    | Crt_Plain
    | Crt_IndexQnA
    | Crt_IndexQnADoc of string //template
    | Crt_QnADoc

///Chat sample enum
type SampleChatType =
    | SM_Plain of bool
    | SM_QnADoc
    | SM_IndexQnA of string
    | SM_IndexQnADoc of string

type Feedback = {
    LogId : string
    ThumbsUpDn : int
    Comment : string option
}
    with static member Default id = {LogId=id; ThumbsUpDn=0; Comment=None}

type Interaction = {
    Id : string
    Name: string option
    Feedback : Feedback option
    Question : string
    SystemMessage : string
    InteractionType: InteractionType
    Messages : InteractionMessage list
    Parameters : InteractionParameters
    Timestamp : DateTime
    IsBuffering : bool
    Notifications : string list
}

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

type SamplePrompt =
    {
        SampleChatType                  : SampleChatType
        SampleSysMsg                    : string
        SampleQuestion                  : string
        SampleMode                      : ExplorationMode
        MaxDocs                         : int
    }

type AuthUser =
    {
        Name:string
        IsAuthorized:bool
        Principal:ClaimsPrincipal
        Roles:Set<string>
        Email:string
    }

type UserState = Authenticated of AuthUser | Unauthenticated

type ServerInitiatedMessages =
    | Srv_Parameters of ServiceSettings
    | Srv_Ia_Delta of string*int*string   //chat id,index,delta
    | Srv_Ia_SetSearch of string*string
    | Srv_Ia_SetDocs of string*Document list
    | Srv_Ia_Done of string*string option //chat id, optional log id, optional error
    | Srv_Ia_SetSubmissionId of string*string
    | Srv_Ia_Notification of string*string //chat id (optional error)
    | Srv_Ia_Session_Loaded of Interaction
    | Srv_Ia_Session_DoneLoading
    | Srv_Error of string
    | Srv_Info of string
    | Srv_IndexesRefreshed of IndexTree list
    | Srv_Ia_SetContents of string*string*bool
    | Srv_SetTemplates of LabeledTemplates list
    | Srv_LoadSamples of (string*SamplePrompt list)
    | Srv_SetConfig of AppConfig
    | Srv_DoneInit of unit
    //code eval support
    | Srv_Ia_SetCode of (string*string option)
    | Srv_Ia_SetPlan of (string*string option)

type ClientInitiatedMessages =
    | Clnt_Connected of string
    | Clnt_RefreshIndexes of ServiceSettings * bool * string list * string // settings * is initial request * template names * metaIndex
    | Clnt_Run_Plain of ServiceSettings*InvocationContext*Interaction
    | Clnt_Run_IndexQnA of ServiceSettings*InvocationContext*Interaction
    | Clnt_Run_IndexQnADoc of ServiceSettings*InvocationContext*Interaction
    | Clnt_Run_QnADoc of ServiceSettings*InvocationContext*Interaction
    | Clnt_UploadChunk of string*byte[]
    | Clnt_ExtractContents of string*string*DocType option
    | Clnt_SearchQuery of ServiceSettings*InvocationContext*Interaction
    | Clnt_Ia_Session_Save of InvocationContext*Interaction
    | Clnt_Ia_Session_LoadAll of InvocationContext
    | Clnt_Ia_Session_ClearAll of InvocationContext
    | Clnt_Ia_Session_Delete of InvocationContext*string
    | Clnt_Ia_Feedback_Submit of InvocationContext*Feedback
    //code eval support
    | Clnt_Run_EvalCode of ServiceSettings*InvocationContext*Interaction*CodeEvalParms


