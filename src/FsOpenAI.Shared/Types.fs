﻿namespace FsOpenAI.Shared
open System
open System.Security.Claims

type DocRef = 
    {
        Text:string
        Embedding:float32[]
        Ref:string
        Title:string
        Id:string
        Relevance: float
        SortOrder : float option
    }

type QueriedDocuments = {SearchQuery:string option; DocRefs: DocRef list }
    with static member Empty = {SearchQuery=None; DocRefs=[]}

type MessageRole = User | Assistant of QueriedDocuments

type InteractionMessage = {MsgId:string; Role:MessageRole; Message: string}
    with
        member this.IsUser =    
            match this.Role with
            | MessageRole.User  -> true
            | _                 -> false

type ExplorationMode = Factual | Exploratory | Creative

type SearchMode = Semantic | Hybrid | Keyword | Auto
    with
        member this.Tooltip =
            match this with
            | SearchMode.Semantic -> "Search with meaning, e.g. 'small' should match 'tiny', 'little', 'not big', etc."
            | SearchMode.Keyword -> "Search using exact keyword matches. Useful for product codes, acronyms, etc. USE only if other modes not effective."
            | SearchMode.Hybrid -> "A mix of Semantic and Keyword"
            | SearchMode.Auto -> "Let the system decide the best mode based on the query text"

type DocType = DT_Pdf | DT_Word | DT_Powerpoint | DT_Excel | DT_Text | DT_RTF | DT_Image | DT_Video | DT_Html

type ModelType = MT_Chat | MT_Logic
    with 
        member this.Text =
            match this with
            | MT_Chat -> "Chat"
            | MT_Logic -> "Logic"
        member this.Tooltip =
            match this with
            | MT_Chat -> "Responds faster to user queries, suitable general use"
            | MT_Logic -> "Takes longer to respond but can perform more complex reasoning (defaults to Chat type if not configured)"

type InteractionParameters =
    {
        Backend             : Backend
        ModelType           : ModelType
        Mode                : ExplorationMode
        MaxTokens           : int
    }

    static member Default =
        {
            Backend = AzureOpenAI
            ModelType = MT_Chat
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
    with 
        member this.isVirtual = this.Idx.isVirtual

type QABag =
    {
        Indexes         : IndexRef list
        MaxDocs         : int
        SearchMode      : SearchMode
    }
    with static member Default =
            {
                Indexes = []
                MaxDocs = 10
                SearchMode = Auto
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
        SearchTerms : string option
    }
    with
        static member Default =
                        {
                            DocumentRef = None
                            DocumentText = None
                            Status = No_Document
                            DocType = None
                            SearchTerms = None
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
    | QnADoc of DocumentContent
    | CodeEval of CodeEvalBag

///Chat sample enum
type SampleChatType =
    | SM_Plain of bool
    | SM_QnADoc
    | SM_IndexQnA of string

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
    Mode : InteractionMode
    Types : InteractionType list
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
    | Srv_Ia_Delta of string*string   //chat id,index,delta
    | Srv_Ia_SetSearch of string*string
    | Srv_Ia_SetDocs of string*DocRef list
    | Srv_Ia_Citations of string*string list
    | Srv_Ia_Done of string*string option //chat id, optional log id, optional error
    | Srv_Ia_SetSubmissionId of string*string
    | Srv_Ia_Notification of string*string //chat id (optional error)
    | Srv_Ia_Reset of string
    | Srv_Ia_Session_Loaded of Interaction
    | Srv_Ia_Session_DoneLoading
    | Srv_Error of string
    | Srv_Info of string
    | Srv_IndexesRefreshed of IndexTree list
    | Srv_Ia_File_Chunk of string*string*bool
    | Srv_Ia_File_Error of string*string
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
    | Clnt_Ia_Doc_Extract of (ServiceSettings*InvocationContext*Backend)*(string*string*DocType option)
    | Clnt_Ia_Session_Save of InvocationContext*Interaction
    | Clnt_Ia_Session_LoadAll of InvocationContext
    | Clnt_Ia_Session_ClearAll of InvocationContext
    | Clnt_Ia_Session_Delete of InvocationContext*string
    | Clnt_Ia_Feedback_Submit of InvocationContext*Feedback
    //code eval support
    | Clnt_Run_EvalCode of ServiceSettings*InvocationContext*Interaction*CodeEvalParms


