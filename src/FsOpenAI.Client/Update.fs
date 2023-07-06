namespace FsOpenAI.Client
open System
open System.Threading.Channels
open Elmish
open Chats
open FSharp.Control

module Update =

    let initModel =    
        {
            chats = Chats.empty
            error = None
            busy = false
            settingsOpen = false 
            highlight_busy = false
            serviceParameters = ApiParameters.Default        
        }

    let newMessage (cntnt:string) = {Role=ChatRole.User; Message=cntnt} 

    let notEmpty (s:string) = String.IsNullOrWhiteSpace s |> not

    let checkBusy model apply = 
        if model.busy then 
            model,
            if model.highlight_busy then Cmd.none else Cmd.ofMsg (HighlightBusy true) 
        else 
            apply() 

    let asyncMsgQueue = 
            let ops = BoundedChannelOptions(1000,
                        SingleReader = true,
                        FullMode = BoundedChannelFullMode.DropNewest,
                        SingleWriter = true)
            Channel.CreateBounded<Message>(ops)

    let queueReader() =
        asyncSeq{
            while true do 
                let! msg = asyncMsgQueue.Reader.ReadAsync().AsTask() |> Async.AwaitTask
                yield msg
        }

    let asyncMessages (model:Model) : (SubId * Subscribe<Message>) list =
        let sub dispatch : IDisposable =
            queueReader()
            |> AsyncSeq.iter(fun msg -> 
                try dispatch msg with ex -> printfn "%A" ex.Message)
            |> Async.Start
            {new IDisposable with member _.Dispose() = ()}
        [["asyncMessages"],sub]

    let submitChat id model () =  
        let comp = 
            Chats.submitChat 
                id 
                model.serviceParameters 
                (fun (id,delta) -> asyncMsgQueue.Writer.TryWrite(Chat_AddDelta (id,delta)) |> ignore)
                (fun (id,exn) -> asyncMsgQueue.Writer.TryWrite(Error exn) |> ignore)
                model.chats
        Async.Start comp
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
        | Chat_AddDelta (id,delta) -> {model with chats = Chats.addDelta id delta model.chats},Cmd.none
        | Chat_Add -> {model with chats = Chats.addChat model.chats},Cmd.none
        | Chat_Remove id -> {model with chats = Chats.removeChat id model.chats},Cmd.none
        | Clear -> checkBusy model <| fun () -> {model with chats=Chats.empty},Cmd.none
        | Error exn -> {model with error = Some exn.Message; busy = false},Cmd.none
        | ClearError -> {model with error = None},Cmd.none
        | SubmitChat id -> checkBusy model <| submitChat id model
        | Reset        -> checkBusy model <| fun () -> {model with chats=Chats.empty},Cmd.none
        | AddDummyContent -> checkBusy model <| addDummyContent message model
        | OpenCloseSettings b -> {model with settingsOpen = b},Cmd.none
        | HighlightBusy t -> highlightBusy model t

