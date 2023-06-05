module FsOpenAI.Client.Model
open System
open Elmish
open Azure.AI.OpenAI

/// The Elmish application's model.
type Model =
    {
        systemPrompt: string
        prompt : string
        chat : ChatMessage list
        error : string option
        busy : bool
        key  : string
        apiKey : string
        resourceGroup: string
    }

let initModel =
    {
        prompt = ""
        systemPrompt = ""
        chat = []
        error = None
        busy = false
        key = ""
        apiKey = ""
        resourceGroup = ""
    }

let newMessage (cntnt:string) = ChatMessage(role=ChatRole.User,content=cntnt)

type Message =
    | SetSystemPrompt of string
    | SetPrompt of string
    | AddItem of ChatMessage
    | SubmitChat
    | GotChat of ChatMessage list
    | Clear 
    | Error of exn
    | ClearError
    | SetKeys of string*string*string
    | Reset 
    | AddDummyContent
    | DeleteChatItem of ChatMessage
    //| RequestKey

let notEmpty (s:string) = String.IsNullOrWhiteSpace s |> not


let update message model =
    printfn $"message: {message}"; 
    match message with
    | SetPrompt s -> {model with prompt = s},Cmd.none
    | SetSystemPrompt s -> {model with systemPrompt = s},Cmd.none
    | AddItem ci -> {model with chat = model.chat @ [ci]},Cmd.none
    | Clear      -> {model with systemPrompt = ""; chat=[]},Cmd.none
    | Error exn -> {model with error = Some exn.Message; busy = false},Cmd.none
    | ClearError -> {model with error = None},Cmd.none
    | SubmitChat -> if notEmpty model.prompt && not model.busy then 
                        let sysMsg = if notEmpty model.systemPrompt then [ChatMessage(ChatRole.System,model.systemPrompt)] else []
                        let chat = model.chat @ [newMessage model.prompt]
                        let chatSubmit = sysMsg @ chat
                        let cmd = Cmd.OfTask.either Api.getCompletions ((model.key,model.apiKey,model.resourceGroup), chatSubmit) GotChat Error
                        {model with prompt=""; chat = chat ; busy=true},cmd
                    else 
                        model,Cmd.none
    | GotChat msgs -> {model with chat = msgs; busy=false},Cmd.none
    | Reset        -> {model with chat=[];systemPrompt="";prompt=""},Cmd.none
    | SetKeys (k,apik,rg) -> {model with key = k; apiKey = apik; resourceGroup=rg; busy=false},Cmd.none
    | AddDummyContent -> 
        {model with
            chat = [for i in 1 .. 20 -> ChatMessage((if i%2=0 then ChatRole.Assistant else ChatRole.User),$"This the text for item {i}")]
        },Cmd.none

    | DeleteChatItem c -> {model with chat = model.chat |> List.filter(fun c' -> not(c'=c))},Cmd.none

