namespace FsOpenAI.Shared

type AppPalette = 
    {
        Primary   : string option
        Secondary : string option
        Tertiary  : string option
        Info      : string option
        Success   : string option
        Warning   : string option
        Error     : string option
        AppBar    : string option
    }
    with 
        static member Default = 
                        {
                            Primary     = None
                            Secondary   = None
                            Tertiary    = None
                            Info        = None
                            Success     = None
                            Warning     = None
                            Error       = None        
                            AppBar      = None
                        }
type Backend = OpenAI | AzureOpenAI

type ModelRef = 
    {
        Backend         : Backend        
        Model           : string
        TokenLimit      : int
    }
    with 
        static member Default = 
            {
                Backend = OpenAI
                Model = "gpt-4o"
                TokenLimit = 2000
            }

type ModelsConfig = 
    {
        ///List of models that can be used to generated embeddings
        EmbeddingsModels : ModelRef list

        ///List of models that will be used for shorter input sequences
        ShortChatModels : ModelRef list

        ///List of models that may be used when input is longer than the context length of short models
        LongChatModels : ModelRef list

        ///List of models that may be used for completion
        CompletionModels : ModelRef list

        LowCostModels : ModelRef list
    }
    with 
            static member Default = 
                {
                    EmbeddingsModels = []
                    ShortChatModels  = []
                    LongChatModels   = [ModelRef.Default]
                    CompletionModels = []
                    LowCostModels    = []
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

type ChatMode = 
    | CM_Plain 
    | CM_IndexQnA 
    | CM_IndexQnADoc 
    | CM_IndexQnASite
    | CM_QnADoc
    | CM_CustCtx
    | CM_TravelSurvey

type AppBarType =
    | AppB_Base of string
    | AppB_Alt of string

type AppConfig = 
    {
        ///Backends that are enabled for this app. First backend in the list is the default
        EnabledBackends : Backend list
        
        ///Set the modes of chat that can be created under this configuration
        EnabledChatModes : (ChatMode*string) list

        ///Default number of docs for new chats
        DefaultMaxDocs : int

        ///List of app roles. If the user's identity provider provides any of the roles, the authenticated user 
        ///is authorized. If the list is empty then any authenticted user is authorized to use this app
        Roles : string list  

        ///AppBar style
        AppBarType : AppBarType option

        ///If true, only authenticated and authorized users will be allowed to 
        ///invoke models
        RequireLogin : bool

        ///Dark theme colors overrides
        PaletteDark : AppPalette option

        ///Light theme colors overrides
        PaletteLight : AppPalette option

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
                EnabledBackends = [OpenAI]
                EnabledChatModes = [CM_Plain,"You are a helpful AI assistant"]
                DefaultMaxDocs = 10
                Roles = []
                RequireLogin = false
                AppBarType = None
                PaletteDark = None
                PaletteLight = None
                Disclaimer = None
                LogoUrl = Some "https://github.com/fwaris/FsOpenAI"
                MetaIndex = None
                IndexGroups = []
                ModelsConfig = ModelsConfig.Default
                AppId = None
                AppName = Some "FsOpenAI"
                PersonaText = None
                PersonaSubText = None
                DatabaseName = C.DFLT_COSMOSDB_NAME
                DiagTableName = None
                SessionTableName = None
            }

