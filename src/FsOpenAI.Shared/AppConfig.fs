namespace FsOpenAI.Shared
open System.Text.Json.Serialization

//[<JsonFSharpConverter(UnionUnwrapFieldlessTags=true)>]
//type Backend = OpenAI | AzureOpenAI

type BackendType = ChatCompletions | ChatCompletionsHarmony

module KnownBackends =
    let [<Literal>] OpenAI = "OpenAI"
    let [<Literal>] AzureOpenAI = "AzureOpenAI"

type Backend = {Name:string; BackendType:BackendType}
    with
        static member Default = {Name=KnownBackends.OpenAI; BackendType=ChatCompletions}
        static member OpenAI = {Name=KnownBackends.OpenAI; BackendType=ChatCompletions}
        static member AzureOpenAI = {Name=KnownBackends.AzureOpenAI; BackendType=ChatCompletions}

type ModelRef =
    {
        Backend         : Backend
        Model           : string
        Label           : string
        TokenLimit      : int
    }
    with
        static member Default =
            {
                Backend = Backend.Default
                Model = "gpt-51"
                Label = "gpt-5"
                TokenLimit = 2000
            }

type ModelsConfig =
    {
        ///List of models that can be used to generated embeddings
        EmbeddingsModels : ModelRef list

        ///List of models that may be used when input is longer than the context length of short models
        ChatModels : ModelRef list

    }
    with
            static member Default =
                {
                    EmbeddingsModels = []
                    ChatModels   = [ModelRef.Default]
                }

type InvocationContext =
    {
        ModelsConfig : ModelsConfig
        AppId        : string option
        User         : string option
    }
    with
        static member Default =
            {
                ModelsConfig = ModelsConfig.Default
                AppId = None
                User = None
            }

type InteractionMode =
    | M_Plain
    | M_Index
    | M_Doc
    | M_Doc_Index
    | M_CodeEval

type AppBarType =
    | AppB_Base of string
    | AppB_Alt of string

type AppConfig =
    {
        ///Hide the '...' menu for the chat view so users are not able to change settings like temperature, etc.
        HideChatSettings : bool

        ///If true, then by default, PDF pages are extracted as images and then converted to text via OCR (can be overridden by user)
        UseORCByDefault : bool

        ///Backends that are enabled for this app. First backend in the list is the default
        EnabledBackends : Backend list

        ///Set the modes of chat that can be created under this configuration
        EnabledChatModes : (InteractionMode*string) list

        ///Default number of docs for new chats
        DefaultMaxDocs : int

        ///List of app roles. If the user's identity provider provides any of the roles, the authenticated user
        ///is authorized. If the list is empty then any authenticated user is authorized to use this app
        Roles : string list

        ///AppBar style
        AppBarType : AppBarType option

        ///If true, only authenticated and authorized users will be allowed to
        ///invoke models
        RequireLogin : bool

        AssistantIcon : string option
        AssistantIconColor : string option

        ///Url to go to when main logo is clicked
        LogoUrl : string option

        ///Name of the application that will show on the browser tab
        AppName: string option

        ///Application identifier that will be logged with each call (along if user name, if authenticated).
        ///This string is logged as 'User' property of the API call
        AppId : string option

        PersonaText : string option
        PersonaSubText : string option
        Disclaimer : string option

        MetaIndex : string option

        ///This application can see indexes that are associated with the given groups.
        ///The index-to-group association is contained in the 'meta' index named in C.META_INDEX constant
        IndexGroups : string list

        ModelsConfig : ModelsConfig

        DatabaseName : string

        DiagTableName : string option

        SessionTableName : string option

    }
    with
        static member Default =
            {
                HideChatSettings = false
                UseORCByDefault = false
                EnabledBackends = [Backend.Default]
                EnabledChatModes = []//M_Plain,"You are a helpful AI assistant"]
                DefaultMaxDocs = 10
                Roles = []
                RequireLogin = false
                AppBarType = None
                AssistantIcon = None
                AssistantIconColor = None
                Disclaimer = None
                LogoUrl = Some "https://github.com/fwaris/FsOpenAI"
                MetaIndex = None
                IndexGroups = []
                ModelsConfig = ModelsConfig.Default
                AppId = None
                AppName = None
                PersonaText = None
                PersonaSubText = None
                DatabaseName = C.DFLT_COSMOSDB_NAME
                DiagTableName = None
                SessionTableName = None
            }

