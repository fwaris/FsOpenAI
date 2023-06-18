module Chats
open System
open FSharp.Control

type ChatRole = User | Assistant
type ChatMessage = {Role:ChatRole; Message: string} 
type ServiceModel = {Model:string; ApiVersion:string} 
type ChatService = Azure of ServiceModel | OpenAI of ServiceModel
    with 
        static member DefaultAzure = Azure {Model="gpt-3.5-turbo"; ApiVersion="2023-06-01-preview"}
        static member DefaultOpenAI = OpenAI {Model="gpt-3.5-turbo"; ApiVersion="v1"}

type ApiMsg = OpenAI_API.Chat.ChatMessage
type ApiRole = OpenAI_API.Chat.ChatMessageRole
type ApiReq = OpenAI_API.Chat.ChatRequest

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
}

let empty = []

let addChat cs = 
    let c = 
        {
            Id = Guid.NewGuid().ToString()
            Name = $"Chat {List.length cs}"
            System = ""
            Messages = [] 
            Parameters = ChatParameters.Default
        }
    cs @ [c]

let removeChat id cs = cs |> List.filter(fun c -> c.Id <> id)

let addMessage (id,msg) cs = 
    cs |> List.map(fun c -> if c.Id = id then {c with Messages = c.Messages @ [msg]} else c)
    
let deleteMessage (id,msg) cs =
    cs |> List.map(fun c -> {c with Messages = c.Messages |> List.filter (fun m -> not(c.Id=id && m=msg))})

let updateSystem (id,sysMsg) cs = cs |> List.map (fun c -> if c.Id = id then {c with System=sysMsg} else c)

let updateName (id,name) cs = cs |> List.map(fun c -> if c.Id = id then {c with Name=c.Name} else c)

let updateParms (id,parms) cs = cs |> List.map(fun c -> if c.Id = id then {c with Parameters = parms} else c)

let private _addDelta delta msgs =
    let lastAssistantMsg = msgs  |> List.tryFindBack(fun x -> x.Role = Assistant)
    let updatedMsg =
        lastAssistantMsg
        |> Option.map(fun m -> {m with Message = m.Message + delta})
        |> Option.defaultValue {Role=Assistant; Message=delta}
    match lastAssistantMsg with
    | None -> msgs @ [updatedMsg]
    | Some m -> updatedMsg::(msgs |> List.rev |> List.skip 1) |> List.rev

let addDelta id delta cs =
    cs
    |> List.map(fun c -> 
        if c.Id = id then 
            {c with Messages = _addDelta delta c.Messages}
        else
            c
    )

let submitChat id (apiParms:Api.ApiParameters) succDispather errorDispatcher cs =
    let c = cs |> List.tryFind (fun x -> x.Id = id)
    match c with
    | None -> async{return ()}
    | Some ch ->
        let messages = 
            seq {
                if String.IsNullOrWhiteSpace ch.System |> not then
                    yield ApiMsg(ApiRole.System, ch.System)
                for m in ch.Messages do
                    let role = 
                        match m.Role with 
                        | User -> ApiRole.User
                        | Assistant -> ApiRole.Assistant
                    yield ApiMsg(role,m.Message)                       
            }
        let caller = 
            match ch.Parameters.Service with 
            | Azure sp -> 
                let clr =
                    OpenAI_API.OpenAIAPI.ForAzure(
                        apiParms.AzureResourceGroup,
                        sp.Model,
                        OpenAI_API.APIAuthentication(apiParms.AzureApiKey))
                clr.ApiVersion <- sp.ApiVersion
                clr
            | OpenAI sp ->               
                new OpenAI_API.OpenAIAPI(
                    apiParms.OpenAIApiKey, 
                    ApiVersion = sp.ApiVersion
                )
        let req = ApiReq(
            Messages = ResizeArray messages,
            Model = (match ch.Parameters.Service with OpenAI p -> p.Model | _ -> null),
            Temperature = ch.Parameters.Temperature,
            PresencePenalty = ch.Parameters.PresencePenalty,
            FrequencyPenalty = ch.Parameters.FrequencyPenalty,
            MaxTokens = ch.Parameters.MaxTokens)            
        async {
            let comp =
                caller.Chat.StreamChatEnumerableAsync(req)
                |> AsyncSeq.ofAsyncEnum
                |> AsyncSeq.iter(fun x -> succDispather (ch.Id,x.Choices.[0].Delta.Content))
            let! rslt = Async.Catch comp
            match rslt with 
            | Choice2Of2 exn -> errorDispatcher (ch.Name,exn)
            | _              -> ()
        }
        