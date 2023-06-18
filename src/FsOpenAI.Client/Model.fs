module FsOpenAI.Client.Model
open System
open Elmish
open Chats


/// The Elmish application's model.
type Model =
    {
        systemPrompt: string
        prompt : string
        chats : Chat list 
        error : string option
        busy : bool
        key  : string
        apiKey : string
        resourceGroup: string
        settingsOpen : bool
        temperature : float
        max_tokens : int
        top_prob : float
        highlight_busy : bool
        serviceParameters : Api.ApiParameters
    }

let initModel =
    {
        prompt = ""
        systemPrompt = ""
        chats = Chats.empty
        error = None
        busy = false
        key = ""
        apiKey = ""
        resourceGroup = ""
        settingsOpen = false
        temperature = 1.0
        max_tokens = 600
        top_prob = 0.95
        highlight_busy = false
        serviceParameters = Api.ApiParameters.Default        
    }

let newMessage (cntnt:string) = {Role=ChatRole.User; Message=cntnt} 

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
    | GotChat of Chat
    | Clear 
    | Error of exn
    | ClearError
    | SetKeys of string*string*string
    | Reset 
    | AddDummyContent
    | DeleteChatItem of string*ChatMessage
    | OpenCloseSettings of bool
    | SetTemperature of float
    | SetMaxTokens of int
    | SetTopProb of float
    | HighlightBusy of bool

let notEmpty (s:string) = String.IsNullOrWhiteSpace s |> not

let checkBusy model apply = 
    if model.busy then 
        model,
        if model.highlight_busy then Cmd.none else Cmd.ofMsg (HighlightBusy true) 
    else 
        apply() 

let submitChat model message () = 
    //if (notEmpty model.prompt && (model.chat.IsEmpty || (List.last model.chat).Role = ChatRole.System))
    //   || (model.chat.IsEmpty |> not && (List.last model.chat).Role=ChatRole.User) then 
    //    let sysMsg = if notEmpty model.systemPrompt then [ChatMessage(ChatRole.System,model.systemPrompt)] else []
    //    let chat = model.chat @ [newMessage model.prompt]
    //    let chatSubmit = sysMsg @ chat
    //    let opts = 
    //        ChatCompletionsOptions(
    //            MaxTokens = model.max_tokens,
    //            Temperature = float32 model.temperature,
    //            NucleusSamplingFactor  = float32 model.top_prob,
    //            FrequencyPenalty = 0.0f,
    //            PresencePenalty = 0.0f        
    //    )
    //    let cmd = Cmd.OfTask.either Api.getCompletions ((model.key,model.apiKey,model.resourceGroup), chatSubmit,opts) GotChat Error
    //    {model with prompt=""; chat = chat ; busy=true},cmd
    //else 
    //    //failwith "To submit, chat history last message should be User role OR User prompt is not empty"           
        model,Cmd.none

let addDummyContent message model () = 
        //{model with
        //    chat = [for i in 1 .. 20 -> ChatMessage((if i%2=0 then ChatRole.Assistant else ChatRole.User),$"This the text for item {i}")]
        //},Cmd.none
        model,Cmd.none

let highlightBusy model t = 
        let delayTask () = 
            async{
                do! Async.Sleep 500
                return false}
        {model with highlight_busy = t}, 
        if not t then 
            Cmd.none 
        else 
            Cmd.OfAsync.perform delayTask () HighlightBusy
            
let update message model =
    printfn $"message: {message}"; 
    match message with
    | Chat_SysPrompt (id,msg) -> {model with chats = Chats.updateSystem (id,msg) model.chats},Cmd.none
    | Chat_AddMsg (id,msg) -> {model with chats = Chats.addMessage (id,msg) model.chats},Cmd.none
    | Chat_DeleteMsg (id,msg) -> {model with chats = Chats.deleteMessage (id,msg) model.chats},Cmd.none
    | Chat_UpdateName (id,n) -> {model with chats = Chats.updateName (id,n) model.chats},Cmd.none
    | Chat_UpdateParms (id,p) -> {model with chats = Chats.updateParms (id,p) model.chats},Cmd.none
    | Chat_Add -> {model with chats = Chats.addChat model.chats},Cmd.none
    | Chat_Remove id -> {model with chats = Chats.removeChat id model.chats},Cmd.none
    | Clear -> checkBusy model <| fun () -> {model with chats=Chats.empty},Cmd.none
    | Error exn -> {model with error = Some exn.Message; busy = false},Cmd.none
    | ClearError -> {model with error = None},Cmd.none
    | SubmitChat id -> checkBusy model <| submitChat model message
    | GotChat msgs -> {model with chat = msgs; busy=false},Cmd.none
    | Reset        -> checkBusy model <| fun () -> {model with chat=[];systemPrompt="";prompt=""},Cmd.none
    | SetKeys (k,apik,rg) -> {model with key = k; apiKey = apik; resourceGroup=rg; busy=false},Cmd.none
    | AddDummyContent -> checkBusy model <| addDummyContent message model
    | DeleteChatItem c -> {model with chat = model.chat |> List.filter(fun c' -> not(c'=c))},Cmd.none
    | OpenCloseSettings b -> {model with settingsOpen = b},Cmd.none
    | SetTemperature f -> {model with temperature = f},Cmd.none
    | SetMaxTokens t -> {model with max_tokens = t},Cmd.none
    | SetTopProb t -> {model with top_prob = t},Cmd.none
    | HighlightBusy t -> highlightBusy model t

