namespace FsOpenAI.Client
open System
open System.Threading.Channels
open Elmish
open FSharp.Control
open Blazored.LocalStorage

module Update =
    open MudBlazor

    let newInteractionTypes = 
        [
            Icons.Material.Outlined.Chat, "New Chat with Azure deployed models", CreateChat AzureOpenAI
            Icons.Material.Outlined.QuestionAnswer, "New Doc. Q&A with Azure deployed models", CreateQA AzureOpenAI
            Icons.Material.Outlined.Chat, "New Chat with OpenAI models", CreateChat OpenAI
            Icons.Material.Outlined.QuestionAnswer,"New Doc. Q&A with OpenAI models", CreateQA OpenAI
        ]

    let addQASample model =
        let cId2,cs = Interactions.addNew (CreateQA AzureOpenAI) (Some "Did T-Mobile buyback stock in 2019?") model.interactions
        let c2 = cs |> List.find (fun c->c.Id = cId2) 
        let c2bag = match c2.InteractionType with QA bag -> bag | _ -> failwith ""
        let iref = model.indexRefs |> List.tryFind (function (Azure n) -> n.Name="tmobile-sec")
        let cs = Interactions.updateQABag cId2 {c2bag with Index=iref; MaxDocs=20} cs
        let parms =  {c2.Parameters with ChatModel="gpt-4-32k"}
        let cs = Interactions.updateParms (cId2,parms) cs
        {model with interactions=cs}
        
    let initModel =    
        let cId,cs = Interactions.addNew (CreateChat AzureOpenAI) (Some "What are the phases of the moon?") Interactions.empty
        let cs = Interactions.updateSystemMsg (cId,"You are a helpful AI") cs
        {
            interactions = cs
            interactionCreateTypes = newInteractionTypes
            indexRefs = []
            error = None
            busy = false
            settingsOpen = Map.empty 
            highlight_busy = false
            serviceParameters = None   
        }

    let checkBusy model apply = 
        if model.busy then 
            model,
            if model.highlight_busy then Cmd.none else Cmd.ofMsg (HighlightBusy true) 
        else 
            apply model

    let submitChat serverDispatch lastMsg id model =  
        if Utils.notEmpty lastMsg then 
            match model.serviceParameters with
            | Some sp -> 
                let chats = 
                    model.interactions
                    |> Interactions.addOrUpdateLastMsg (id,lastMsg)
                    |> Interactions.addMessage (id,Interactions.Interaction.newAsstantMessage "")
                    |> Interactions.startBuffering id
                let model = {model with interactions = chats; error=None}
                let ch = model.interactions |> List.find(fun x->x.Id=id)
                match ch.InteractionType with
                | QA _ -> serverDispatch (Clnt_StreamAnswer(sp,ch))
                | Chat _ -> serverDispatch (Clnt_StreamChat(sp,ch))
                model,Cmd.none
            | None -> model,Cmd.ofMsg(ShowInfo "Service configuration not yet received from server")
        else 
            model,Cmd.ofMsg(ShowInfo "Question is empty")

    let completeChat id err model =
        let cs = Interactions.endBuffering id (Option.isSome err) model.interactions
        let model = {model with interactions = cs}
        let cmd = err |> Option.map(fun e -> Cmd.ofMsg(ShowError e)) |> Option.defaultValue Cmd.none
        model,cmd

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
      
    let pingServer serverDispatch =
        async{
            do! Async.Sleep 100
            printfn "sending connect to server"
            serverDispatch (ClientInitiatedMessages.Clnt_Connected "me")
        }
        |> Async.StartImmediate

    let addDefaultModel f1 f2 (sParms:ServiceSettings option) chatParms =
        sParms
        |> Option.map(fun p -> 
            match chatParms.Backend with AzureOpenAI -> p.AZURE_OPENAI_MODELS | OpenAI -> p.OPENAI_MODELS
            |> Option.bind(fun d -> List.tryHead (f1 d))
            |> Option.map (fun m -> f2 chatParms m)
            |> Option.defaultValue chatParms)
        |> Option.defaultValue chatParms
        
    let MAX_INTERACTIONS = 15
    let checkAddInteraction ctype model =
        if model.interactions.Length < MAX_INTERACTIONS then
            let id,cs = Interactions.addNew ctype None model.interactions      
            let c = cs |> List.find (fun c -> c.Id=id)
            let sParms = model.serviceParameters
            let cParms =
                c.Parameters
                |> addDefaultModel (fun d -> d.CHAT)       (fun p m -> {p with ChatModel = m})        sParms
                |> addDefaultModel (fun d -> d.COMPLETION) (fun p m -> {p with CompletionsModel = m}) sParms
                |> addDefaultModel (fun d -> d.EMBEDDING)  (fun p m -> {p with EmbeddingsModel = m})  sParms
            let cs = Interactions.updateParms (id,cParms) cs
            {model with interactions = cs},Cmd.none
        else
            model,Cmd.ofMsg (ShowInfo "Max number of tabs reached")

    let refreshIndexes serverDispath initial model  =
        match model.serviceParameters with
        | Some sp -> serverDispath (Clnt_RefreshIndexes (sp,initial)); {model with busy=true},Cmd.none
        | None    -> model,Cmd.ofMsg(IndexesRefreshed([],Some "Service Parameters missing"))

    let openClose id model =  
        {model with 
            settingsOpen = 
                model.settingsOpen 
                |> Map.tryFind id 
                |> Option.map(fun b -> model.settingsOpen |> Map.add id (not b))
                |> Option.defaultWith(fun _ -> model.settingsOpen |> Map.add id true)
        }

    let postInit (idxs,err,initial) = 
        let idxMsg = Cmd.ofMsg(IndexesRefreshed(idxs,err))
        let postInit = if initial && err.IsNone then [idxMsg; Cmd.ofMsg(AddSamples); Cmd.ofMsg GetOpenAIKey] else [idxMsg; Cmd.ofMsg GetOpenAIKey]
        Cmd.batch postInit

    let getKeyFromLocal (localStore:ILocalStorageService) model =
        match model.serviceParameters with 
        | Some p when p.OPENAI_KEY.IsNone || Utils.isEmpty p.OPENAI_KEY.Value ->
            let t() = task{return! localStore.GetItemAsync<string> C.LS_OPENAI_KEY}
            model,Cmd.OfTask.either t () SetOpenAIKey IgnoreError
        | _ -> model,Cmd.none

    //if there is an exception when processing a message, the Elmish message loop terminates
    let update (localStore:ILocalStorageService) (snkbar:ISnackbar) serverDispatch message model =
        printfn "%A" message
        match message with
        | Chat_SysPrompt (id,msg) -> {model with interactions = Interactions.updateSystemMsg (id,msg) model.interactions},Cmd.none
        | Ia_AddMsg (id,msg) -> {model with interactions = Interactions.addMessage (id,msg) model.interactions},Cmd.none
        | Ia_UpdateLastMsg (id,msg) -> {model with interactions = Interactions.addOrUpdateLastMsg(id,msg) model.interactions},Cmd.none
        | Ia_DeleteMsg (id,msg) -> {model with interactions = model.interactions |> Interactions.tryDeleteMessage (id,msg)},Cmd.none
        | Ia_UpdateName (id,n) -> {model with interactions = Interactions.updateName (id,n) model.interactions},Cmd.none
        | Ia_UpdateParms (id,p) -> {model with interactions = Interactions.updateParms (id,p) model.interactions},Cmd.none
        | Ia_AddDelta (id,delta) -> {model with interactions = Interactions.addDelta id delta model.interactions},Cmd.none
        | Ia_Completed(id,err) -> completeChat id err model
        | Ia_Add ctype -> checkAddInteraction ctype model
        | Ia_Notification (id,note) -> {model with interactions = Interactions.updateNotification id note model.interactions},Cmd.none
        | Ia_UpdateQaBag (id,bag) -> {model with interactions = Interactions.updateQABag id bag model.interactions},Cmd.none
        | Ia_Remove id -> {model with interactions = Interactions.remove id model.interactions},Cmd.none
        | Clear -> checkBusy model <| fun _-> {model with interactions=Interactions.empty},Cmd.none
        | Error exn -> model,Cmd.ofMsg (ShowError exn.Message)
        | ShowError str -> snkbar.Add(str,severity=Severity.Error) |> ignore;model,Cmd.none
        | ShowInfo str -> snkbar.Add(str) |> ignore; model,Cmd.none
        | Nop () -> model,Cmd.none
        | ClearError -> {model with error = None},Cmd.none
        | SubmitInteraction (id,lastMsg) -> checkBusy model <| (submitChat serverDispatch lastMsg id)
        | Reset        -> checkBusy model <| fun _ -> {model with interactions=Interactions.empty},Cmd.none
        | OpenCloseSettings id -> openClose id model, Cmd.none
        | HighlightBusy t -> highlightBusy model t
        | SetServiceParms p -> {model with serviceParameters = Some p},Cmd.none
        | Started -> pingServer serverDispatch; {model with busy=true},Cmd.none
        | AddSamples -> addQASample model,Cmd.none
        | RefreshIndexes initial -> checkBusy model <| refreshIndexes serverDispatch initial
        | IndexesRefreshed (idxs,err) -> {model with indexRefs = idxs; busy=false}, match err with Some e -> Cmd.ofMsg(ShowInfo e) | _ -> Cmd.none
        | GetOpenAIKey -> getKeyFromLocal localStore model
        | SetOpenAIKey key -> {model with serviceParameters = model.serviceParameters |> Option.map (fun p -> {p with OPENAI_KEY = Some key})},Cmd.none
        | UpdateOpenKey key -> model,Cmd.batch [Cmd.ofMsg (SetOpenAIKey key); Cmd.ofMsg (SaveToLocal(C.LS_OPENAI_KEY,key))]
        | SaveToLocal (k,v) -> localStore.SetItemAsStringAsync(k,v) |> ignore; model,Cmd.none
        | IgnoreError ex -> model,Cmd.none
        | FromServer (Srv_Parameters p) -> {model with serviceParameters=Some p; busy=false},Cmd.ofMsg(RefreshIndexes true)
        | FromServer (Srv_Ia_Delta(id,i,d)) -> model,Cmd.ofMsg(Ia_AddDelta(id,d))
        | FromServer (Srv_Ia_Done(id,err)) -> model, Cmd.ofMsg(Ia_Completed(id,err))
        | FromServer (Srv_Error err) -> model,Cmd.ofMsg (ShowError err)
        | FromServer (Srv_Info err) -> model,Cmd.ofMsg (ShowInfo err)
        | FromServer (Srv_IndexesRefreshed (idxs,err,initial)) -> {model with busy=false}, postInit (idxs,err,initial)
        | FromServer (Srv_Ia_Notification (id,note)) -> model,Cmd.ofMsg(Ia_Notification(id,note))
