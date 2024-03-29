﻿namespace FsOpenAI.Client 
open MudBlazor

type AppPalette = 
    {
        Primary : string option
        Secondary : string option
        Tertiary : string option
        Info : string option
        Success : string option
        Warning : string option
        Error : string option
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
                        }
type Backend = OpenAI | AzureOpenAI

type ModelRef = 
    {
        Backend         : Backend        
        Model           : string
        TokenLimit      : int
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
    }
    with 
            static member Default = 
                {
                    EmbeddingsModels = []
                    ShortChatModels = []
                    LongChatModels = []
                    CompletionModels = []
                }

type AppConfig = 
    {
        ///Backends that are enabled for this app. First backend in the list is the default
        EnabledBackends : Backend list
        
        ///Enable/disable plain chat mode
        EnableVanillaChat : bool

        ///Enable disable 'doc query' mode
        EnableDocQuery : bool

        ///Default system message for new chats
        DefaultSystemMessage : string

        ///Default number of docs for new chats
        DefaultMaxDocs : int

        ///List of app roles. If the user's identity provider provides any of the roles, the authenticated user 
        ///is authorized. If the list is empty then any authenticted user is authorized to use this app
        Roles : string list  

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

        PersonaText : string option

        MetaIndex : string option

        ///This application can see indexes that are associated with the given groups.
        ///The index-to-group association is contained in the 'meta' index named in C.META_INDEX constant
        IndexGroups : string list

        ModelsConfig : ModelsConfig

    }
    with 
        static member Default = 
            {
                EnabledBackends = [OpenAI]
                EnableDocQuery = true
                EnableVanillaChat = true
                DefaultSystemMessage = "You are a helpful AI assistant"
                DefaultMaxDocs = 10
                Roles = []
                RequireLogin = false
                PaletteDark = None
                PaletteLight = None
                LogoUrl = Some "https://github.com/fwaris/FsOpenAI"
                MetaIndex = None
                IndexGroups = []
                ModelsConfig = ModelsConfig.Default
                AppName = Some "FsOpenAI"
                PersonaText = None
            }


module AppConfig =
    let setColors (ap:AppPalette) (p:Palette) =
        ap.Primary |> Option.iter (fun c -> p.Primary <- Utilities.MudColor(c))
        ap.Secondary |> Option.iter (fun c -> p.Secondary <- Utilities.MudColor(c))
        ap.Tertiary |> Option.iter (fun c -> p.Tertiary <- Utilities.MudColor(c))
        ap.Info|> Option.iter (fun c -> p.Info <- Utilities.MudColor(c))
        ap.Success |> Option.iter (fun c -> p.Success <- Utilities.MudColor(c))
        ap.Warning |> Option.iter (fun c -> p.Warning <- Utilities.MudColor(c))
        ap.Error |> Option.iter (fun c -> p.Error <- Utilities.MudColor(c))

    let toTheme (appConfig:AppConfig) = 
        let pLight = new PaletteLight()
        let pDark = new PaletteDark()
        appConfig.PaletteDark |> Option.iter(fun p -> setColors p pDark)
        appConfig.PaletteLight |> Option.iter(fun p -> setColors p pLight)
        let th = MudTheme()
        th.PaletteDark <- pDark
        th.Palette <- pLight
        th

