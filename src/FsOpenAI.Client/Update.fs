namespace FsOpenAI.Client
open System
open System.Threading.Channels
open Elmish
open FSharp.Control
open Blazored.LocalStorage
open FsOpenAI.Client.Interactions

module Update =
    open MudBlazor

    let newInteractionTypes = 
        [
            Icons.Material.Outlined.Chat, "New Chat with Azure deployed models", CreateChat AzureOpenAI
            Icons.Material.Outlined.QuestionAnswer, "New Doc. Q&A with Azure deployed models", CreateQA AzureOpenAI
            Icons.Material.Outlined.Chat, "New Chat with OpenAI models", CreateChat OpenAI
            Icons.Material.Outlined.QuestionAnswer,"New Doc. Q&A with OpenAI models", CreateQA OpenAI
        ]

    let addSamples model =
        let backend = 
            model.serviceParameters 
            |> Option.map(fun p -> if not(p.AZURE_OPENAI_ENDPOINTS.IsEmpty) then AzureOpenAI else OpenAI) 
            |> Option.defaultValue OpenAI

        let serchConfigured =
            model.serviceParameters
            |> Option.map(fun x -> not(x.AZURE_SEARCH_ENDPOINTS.IsEmpty))
            |> Option.defaultValue false

        let chatModel = match backend with OpenAI -> "gpt-3.5-turbo-16k" | AzureOpenAI -> "gpt-4-32k"

        //sample 1
        let cId,cs = Interactions.addNew (CreateChat backend) (Some "Given me 10 best tips for effective story telling in a corporate setting.") model.interactions
        let cs = Interactions.updateParms (cId,{(List.last cs).Parameters with ChatModel=chatModel}) cs

        let cs = 
            if serchConfigured then 
                //sample 2
                let cId2,cs = Interactions.addNew (CreateQA backend) (Some "Summarize the GAAP related policy decisions make by Verizon over the past few years") cs
                let cs = Interactions.updateParms (cId2,{(List.last cs).Parameters with ChatModel=chatModel}) cs
                let bag = match (List.last cs).InteractionType with QA bag -> bag | _ -> failwith ""
                let iref = model.indexRefs |> List.tryFind (function (Azure n) -> n.Name="verizon-sec") |> Option.map(fun x -> [x]) |> Option.defaultValue []
                Interactions.updateQABag cId2 {bag with Indexes=iref; MaxDocs=20} cs            
            else 
                cs
        {model with interactions=cs}
        
    let initModel =    
        {
            interactions = []
            interactionCreateTypes = newInteractionTypes
            indexRefs = []
            error = None
            busy = false
            settingsOpen = Map.empty 
            highlight_busy = false
            serviceParameters = None   
            darkTheme = true
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
                    |> Interactions.updateAndCloseLastUserMsg (id,lastMsg)
                    |> Interactions.addMessage (id,Interaction.newAsstantMessage "")
                    |> Interactions.clearNotifications id 
                    |> Interactions.clearDocuments id
                    |> Interactions.startBuffering id
                let model = {model with interactions = chats; error=None}
                let ch = model.interactions |> List.find(fun x->x.Id=id)
                match ch.InteractionType with
                | QA _   -> serverDispatch (Clnt_StreamAnswer(sp,ch))
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
        let postInit = 
            if initial then
                [idxMsg; Cmd.ofMsg GetOpenAIKey; Cmd.ofMsg Ia_Load]   //run at startup
            else 
                [idxMsg]
        Cmd.batch postInit

    let getKeyFromLocal (localStore:ILocalStorageService) model =
        match model.serviceParameters with 
        | Some p when p.OPENAI_KEY.IsNone || Utils.isEmpty p.OPENAI_KEY.Value ->
            let t() = task{return! localStore.GetItemAsync<string> C.LS_OPENAI_KEY}
            model,Cmd.OfTask.either t () SetOpenAIKey IgnoreError
        | _ -> model,Cmd.none

    let updateDocs (id,docs) model =
        model.interactions
        |> List.tryFind(fun c -> c.Id = id)
        |> Option.bind (fun c -> match c.InteractionType with QA bag -> Some bag | _ -> None)
        |> Option.map(fun bag -> Interactions.updateQABag id {bag with Documents = docs} model.interactions)
        |> Option.map(fun cs -> {model with interactions = cs})
        |> Option.defaultValue model

    let saveChats (model,(localStore:ILocalStorageService)) =
        let cs = model.interactions |> List.map Interaction.clearDocuments 
        task {
            do! localStore.SetItemAsync(C.CHATS,cs)
            return "Chats saved"
        }

    let loadChats (localStore:ILocalStorageService) =
        task {
            try               
                let! haveChats = localStore.ContainKeyAsync(C.CHATS)
                if haveChats then
                    let! cs = localStore.GetItemAsync<Interaction list>(C.CHATS)              
                    return cs
                else
                    return []
            with ex ->
                return failwith $"Unable to load saved chats: '{ex.Message}'"
        }

    let deleteSavedChats (localStore:ILocalStorageService) =
        task {
            try               
                let! haveChats = localStore.ContainKeyAsync(C.CHATS)
                if haveChats then
                    do! localStore.RemoveItemAsync(C.CHATS)
                return "Saved chats deleted"
            with ex ->                
                return failwith $"Unable to delete saved chats: '{ex.Message}'"
        }

    //if there is an exception when processing a message, the Elmish message loop terminates
    let update (localStore:ILocalStorageService) (snkbar:ISnackbar) serverDispatch message model =
        printfn "%A" message
        match message with
        | Chat_SysPrompt (id,msg) -> {model with interactions = Interactions.updateSystemMsg (id,msg) model.interactions},Cmd.none
        | Ia_Save -> model, Cmd.OfTask.either saveChats (model,localStore) ShowInfo Error
        | Ia_Load -> model, Cmd.OfTask.either loadChats localStore Ia_Loaded Error
        | Ia_Loaded cs -> {model with interactions = cs},if cs.IsEmpty then Cmd.ofMsg AddSamples else Cmd.none
        | Ia_ClearChats -> {model with interactions=[]},Cmd.ofMsg(ShowInfo "Chats cleared")
        | Ia_DeleteSavedChats -> model,Cmd.OfTask.either deleteSavedChats localStore ShowInfo Error
        | Ia_AddMsg (id,msg) -> {model with interactions = Interactions.addMessage (id,msg) model.interactions},Cmd.none
        | Ia_UpdateLastMsg (id,msg) -> {model with interactions = Interactions.setLastUserMessage(id,msg) model.interactions},Cmd.none
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
        | AddSamples -> addSamples model,Cmd.none
        | RefreshIndexes initial -> checkBusy model <| refreshIndexes serverDispatch initial
        | IndexesRefreshed (idxs,err) -> {model with indexRefs = idxs; busy=false}, match err with Some e -> Cmd.ofMsg(ShowInfo e) | _ -> Cmd.none
        | GetOpenAIKey -> getKeyFromLocal localStore model
        | SetOpenAIKey key -> {model with serviceParameters = model.serviceParameters |> Option.map (fun p -> {p with OPENAI_KEY = Some key})},Cmd.none
        | UpdateOpenKey key -> model,Cmd.batch [Cmd.ofMsg (SetOpenAIKey key); Cmd.ofMsg (SaveToLocal(C.LS_OPENAI_KEY,key))]
        | SaveToLocal (k,v) -> localStore.SetItemAsStringAsync(k,v) |> ignore; model,Cmd.none
        | ToggleTheme -> {model with darkTheme = not model.darkTheme},Cmd.none
        | IgnoreError ex -> model,Cmd.none
        | FromServer (Srv_Parameters p) -> {model with serviceParameters=Some p; busy=false},Cmd.ofMsg(RefreshIndexes true)
        | FromServer (Srv_Ia_Delta(id,i,d)) -> model,Cmd.ofMsg(Ia_AddDelta(id,d))
        | FromServer (Srv_Ia_Done(id,err)) -> model, Cmd.ofMsg(Ia_Completed(id,err))
        | FromServer (Srv_Error err) -> model,Cmd.ofMsg (ShowError err)
        | FromServer (Srv_Info err) -> model,Cmd.ofMsg (ShowInfo err)
        | FromServer (Srv_IndexesRefreshed (idxs,err,initial)) -> {model with busy=false}, postInit (idxs,err,initial)
        | FromServer (Srv_Ia_Notification (id,note)) -> model,Cmd.ofMsg(Ia_Notification(id,note))
        | FromServer (Srv_Ia_SetDocs (id,docs)) -> updateDocs (id,docs) model, Cmd.none