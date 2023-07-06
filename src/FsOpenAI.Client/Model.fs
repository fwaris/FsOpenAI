namespace FsOpenAI.Client 
open System
open OpenAI_API

type ApiParameters = 
    {
        OpenAIApiKey : string
        AzureApiKey : string
        AzureResourceGroup : string        
    }
    static member Default = 
        {
            OpenAIApiKey    = ""
            AzureApiKey = ""
            AzureResourceGroup = ""
        }

type ChatRole = User | Assistant
type ChatMessage = {Role:ChatRole; Message: string} 
type ServiceModel = {Model:string; ApiVersion:string} 

type ChatService = Azure of ServiceModel | OpenAI of ServiceModel
    with 
        static member DefaultAzure = Azure {Model="gpt-3.5-turbo"; ApiVersion="2023-06-01-preview"}
        static member DefaultOpenAI = OpenAI {Model="gpt-3.5-turbo"; ApiVersion="v1"}

type ChatParameters = 
    {
        Service     : ChatService
        Temperature : float
        PresencePenalty : float
        FrequencyPenalty : float
        MaxTokens : int 

    }
    static member Default = 
        {
            Service = ChatService.DefaultOpenAI
            Temperature = 1.0      //0.0 to 2.0
            PresencePenalty = 0.0  //-2.0 to +2.0
            FrequencyPenalty = 0.0 //-2.0 to +2.0
            MaxTokens = 1000            
        }

type Chat = { 
    Id : string
    Name: string
    System: string;
    Messages : ChatMessage list
    Parameters : ChatParameters
    Timestamp : DateTime
}

type Model =
    {
        chats : Chat list 
        error : string option
        busy : bool
        settingsOpen : bool
        highlight_busy : bool
        serviceParameters : ApiParameters
    }


type Message =
    | Chat_SysPrompt of string * string
    | Chat_AddMsg of string * ChatMessage    
    | Chat_AddDelta of string * string
    | Chat_DeleteMsg of string * ChatMessage
    | Chat_UpdateName of string * string
    | Chat_UpdateParms of string * ChatParameters
    | Chat_Add 
    | Chat_Remove of string 
    | SubmitChat of string
    | Clear 
    | Error of exn
    | ClearError
    | Reset 
    | AddDummyContent
    | OpenCloseSettings of bool
    | HighlightBusy of bool
