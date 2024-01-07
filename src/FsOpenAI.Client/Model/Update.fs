namespace FsOpenAI.Client
open System
open System.IO
open System.Net.Http
open Elmish
open FSharp.Control
open Blazored.LocalStorage
open FsOpenAI.Client.Interactions
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Authorization
open Microsoft.AspNetCore.Components.WebAssembly.Authentication
open MudBlazor
open System.Threading.Tasks
open System.Security.Claims

module Update =
        
    let initModel =    
        {
            flashBanner = true
            interactions = []
            templates = []
            appConfig = AppConfig.Default
            indexTrees = []
            error = None
            busy = false
            tempChatSettings = Map.empty
            settingsOpen = Map.empty 
            serviceParameters = None   
            darkTheme = true
            theme = new MudBlazor.MudTheme()
            user = Unauthenticated
            page = Home
            photo = None
            selectedChatId = None
            tabsUp = true
        }

    let selectedChat model = 
        match model.selectedChatId with 
        | Some id -> model.interactions |> List.tryFind (fun c -> c.Id = id)
        | None    -> None

    let checkBusy model apply = 
        if model.busy then 
            model,Cmd.none
        else 
            apply model


    module Auth = 
        let initiateLogin() =
            async{
                do! Async.Sleep 1000    //post login after delay so user can see flash message
                return LoginLogout
            }

        let checkAuth model apply  =
            match model.appConfig.RequireLogin, model.user with
            | true,Unauthenticated                         -> model, Cmd.batch [Cmd.ofMsg (FlashInfo "Authenticating..."); (Cmd.OfAsync.perform initiateLogin () id) ]
            | true,Authenticated u when not u.IsAuthorized -> model, Cmd.ofMsg (ShowInfo "User not authorized")
            | _,_                                          -> apply model

        let checkAuthFlip apply model  = checkAuth model apply 

        let isAuthorized model =        
            match model.appConfig.RequireLogin, model.user with
            | true,Unauthenticated                         -> false
            | true,Authenticated u when not u.IsAuthorized -> false
            | _,_                                          -> true

        //Ultimately takes the user to the login/logout page of AD 
        let loginLogout (navMgr:NavigationManager) model =
            match model.user with 
            | Unauthenticated -> navMgr.NavigateToLogin("authentication/login")
            | Authenticated _  -> navMgr.NavigateToLogout("authentication/logout")           

        let postAuth model (claimsPrincipal:ClaimsPrincipal option) =
            match claimsPrincipal with
            | None                                        -> {model with user=Unauthenticated}, Cmd.none
            | Some p when  not p.Identity.IsAuthenticated -> {model with user=Unauthenticated}, Cmd.none
            | Some p ->      
                let claims = 
                    p.Claims 
                    |> Seq.tryFind(fun x ->x.Type="roles")
                    |> Option.map(fun x->Text.Json.JsonSerializer.Deserialize<string list>(x.Value))
                    |> Option.defaultValue []
                    |> set
                let roles = model.appConfig.Roles |> set
                let userRoles = Set.intersect claims roles
                let hasAuth = model.appConfig.Roles.IsEmpty || not userRoles.IsEmpty
                let user = {Name=p.Identity.Name; IsAuthorized=hasAuth; Principal=p; Roles=userRoles}            
                let model = {model with user=UserState.Authenticated user}
                let cmds = 
                    Cmd.batch 
                        [
                            if not hasAuth then 
                                yield Cmd.ofMsg (ShowError "User not authorized")
                            yield Cmd.ofMsg GetUserDetails
                        ]
                model,cmds

    module TmpState = 
        let openClose id model =  
            {model with 
                settingsOpen = 
                    model.settingsOpen 
                    |> Map.tryFind id 
                    |> Option.map(fun b -> model.settingsOpen |> Map.add id (not b))
                    |> Option.defaultWith(fun _ -> model.settingsOpen |> Map.add id true)
            }

        let updateChatTempState id model fSet defaultValue = 
            let st = 
                model.tempChatSettings
                |> Map.tryFind id 
                |> Option.map fSet
                |> Option.defaultValue defaultValue
                |> fun cs -> Map.add id cs model.tempChatSettings
            {model with tempChatSettings=st}
        
        let toggleChatSettings id model = 
            updateChatTempState id model 
                (fun s -> {s with SettingsOpen = not s.SettingsOpen}) 
                {TempChatState.Default with SettingsOpen=true}

        let toggleChatDocs (id,msgId) model =
            updateChatTempState id model 
                (fun s -> {s with DocsOpen = msgId}) 
                {TempChatState.Default with DocsOpen=msgId}

        let toggleDocDetails id model =
            updateChatTempState id model 
                (fun s -> {s with DocDetailsOpen = not s.DocDetailsOpen}) 
                {TempChatState.Default with DocDetailsOpen=true}

        let togglePrompts id model =
            updateChatTempState id model 
                (fun s -> {s with PromptsOpen = not s.PromptsOpen}) 
                {TempChatState.Default with PromptsOpen=true}

        let toggleIndex id model =
            updateChatTempState id model 
                (fun s -> {s with IndexOpen = not s.IndexOpen}) 
                {TempChatState.Default with IndexOpen=true}
        
        let toggleSysMsg id model =
            updateChatTempState id model 
                (fun s -> {s with SysMsgOpen = not s.SysMsgOpen}) 
                {TempChatState.Default with SysMsgOpen=true}

        let isDocsOpen model = 
            let selChat = selectedChat model
            let docs = 
                selChat
                |> Option.bind (fun chat -> 
                    model.tempChatSettings
                    |> Map.tryFind chat.Id
                    |> Option.bind (_.DocsOpen)
                    |> Option.bind(fun msgId ->                     
                        chat.Messages
                        |> List.filter(fun m -> m.MsgId=msgId)
                        |> List.tryHead 
                        |> Option.map(fun m -> match m.Role with Assistant s -> s.Docs | _ -> failwith "unexpected"))
                )
                |> Option.defaultValue []
            selChat |> Option.map (fun c -> c.Id),docs

        let chatSettingsOpen id model =
            model.tempChatSettings
            |> Map.tryFind id
            |> Option.map(fun x -> x.SettingsOpen)
            |> Option.defaultValue false

        let isDocDetailsOpen id model =
            model.tempChatSettings
            |> Map.tryFind id
            |> Option.map(fun x -> x.DocDetailsOpen)
            |> Option.defaultValue false

        let isPromptsOpen id model =
            model.tempChatSettings
            |> Map.tryFind id
            |> Option.map(fun x -> x.PromptsOpen)
            |> Option.defaultValue false

        let isIndexOpen id model =
            model.tempChatSettings
            |> Map.tryFind id
            |> Option.map(fun x -> x.IndexOpen)
            |> Option.defaultValue false

        let isSysMsgOpen id model =
            model.tempChatSettings
            |> Map.tryFind id
            |> Option.map(fun x -> x.SysMsgOpen)
            |> Option.defaultValue false

    module IO = 

        //flat set of all nodes rooted at the given node
        let rec subTree acc (t:IndexTree) =        
            let acc = Set.add t acc
            (acc,t.Children) ||> List.fold subTree

        let expandIdxRefs model (idxs:IndexRef list) =
            let treeMap = Init.flatten model.indexTrees |> List.map(fun x -> x.Idx,x) |> Map.ofList                
            let rec loop acc (idx: IndexRef) =
                if idx.isVirtual then               //if index is virtual then loop over its children to add non-virtual parents to the set
                    let subT = subTree Set.empty treeMap.[idx] |> Set.map(_.Idx)
                    let children = Set.remove idx subT
                    (acc,children) ||> Set.fold loop
                else
                    acc |> Set.add idx              //if index is not virtual then don't include children. assume index contains the contents of all children also
            (Set.empty,idxs) ||> List.fold loop

        let refreshIndexes serverDispath initial model  =    
            let metaIndex = model.appConfig.MetaIndex|>Option.defaultValue C.DEFAULT_META_INDEX
            let msgf sp = Clnt_RefreshIndexes (sp,initial,model.appConfig.IndexGroups,metaIndex)
            match model.serviceParameters with
            | Some sp -> serverDispath (msgf sp); {model with busy=true},Cmd.none
            | None    -> model,Cmd.ofMsg(ShowInfo "Service Parameters missing")

        let getKeyFromLocal (localStore:ILocalStorageService) model =
            match model.serviceParameters with 
            | Some p when p.OPENAI_KEY.IsNone || Utils.isEmpty p.OPENAI_KEY.Value ->
                let t() = task{return! localStore.GetItemAsync<string> C.LS_OPENAI_KEY}
                model,Cmd.OfTask.either t () SetOpenAIKey IgnoreError
            | _ -> model,Cmd.none

        let saveChats (model,(localStore:ILocalStorageService)) =
            let cs = model.interactions |> List.map Interaction.preSerialize
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
                    return failwith $"Unable to load saved chats. Likely chat format has changed and saved chats are no longer valid as per new format. Save chats again. Error: '{ex.Message}'"
            }

        let loadKey<'t> key (localStore:ILocalStorageService) =
            task {
                try               
                    let! haveKey = localStore.ContainKeyAsync(key)
                    if haveKey then
                        let! item = localStore.GetItemAsync<'t>(key)
                        return item
                    else
                        return Unchecked.defaultof<'t>
                with ex ->
                    return failwith $"Unable to load saved key of type {typeof<'t>}: '{ex.Message}'"                
            }

        let loadTheme (localStore:ILocalStorageService) = loadKey<bool> C.DARK_THEME localStore
        let loadTabsUp (localStore:ILocalStorageService) = loadKey<bool> C.TABS_UP localStore

        let loadUIState (localStore:ILocalStorageService) =
            task {
                let! darkTheme = loadTheme localStore
                let! tabsUp = loadTabsUp localStore
                return darkTheme,tabsUp
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

        let purgeLocalStorage (localStore:ILocalStorageService) =
            task {
                try               
                    do! localStore.ClearAsync()
                    return "Local storage cleared"
                with ex ->                
                    return failwith $"Unable to clear local storage: '{ex.Message}'"
            }

        let loadFile (id:string,model,serverCall:_->Task)  =
            task {    
                let fileId = Utils.newId().Replace('/','-').Replace('\\','-')
                let ch = model.interactions |> List.find (fun c -> c.Id = id)
                let dbag = Interaction.docBag ch 
                let file = match dbag.Document.DocumentRef with Some f -> f | _ -> failwith "No file selected"            
                use str = file.OpenReadStream(maxAllowedSize = C.MAX_UPLOAD_FILE_SIZE)
                let buff = Array.zeroCreate 1024
                let mutable read = 0
                let! r = str.ReadAsync(buff,0,buff.Length) 
                read <- r
                while (read > 0) do                
                    //printfn $"read {read}"
                    if read = buff.Length then 
                        do! serverCall (Clnt_UploadChunk (fileId,buff))                        
                    else
                        do! serverCall (Clnt_UploadChunk(fileId,buff.[0..read-1]))                    
                    let! r = str.ReadAsync(buff,0,buff.Length) 
                    read <- r
                return (id,fileId)
            }

    let submitChat serverDispatch prompt id model =  
        if Utils.notEmpty prompt then 
            match model.serviceParameters with
            | Some sp -> 
                let chats = 
                    model.interactions
                    |> Interactions.setUserMessage id prompt
                    |> Interactions.setQuestion id ""
                    |> Interactions.addMessage id (Interaction.newAsstantMessage "")
                    |> Interactions.clearNotifications id 
                    |> Interactions.startBuffering id
                let model = {model with interactions = chats; error=None}
                //chat changes below are for submission only - the state of chat in UI is not affected
                let ch = 
                    model.interactions 
                    |> List.find(fun x->x.Id=id) 
                    |> Interaction.preSerialize
                let idxs = Interaction.getIndexs ch
                let ch = 
                    if List.isEmpty idxs then 
                        ch 
                    else 
                        ch |> Interaction.setIndexes (IO.expandIdxRefs model idxs |> Set.toList) //expand index list to include child indexes, if needed
                let mcfg = model.appConfig.ModelsConfig
                match ch.InteractionType with
                | QA _   -> serverDispatch (Clnt_ProcessQA(sp,mcfg,ch))
                | Chat _ -> serverDispatch (Clnt_ProcessChat(sp,mcfg,ch))
                | DocQA _ -> serverDispatch (Clnt_ProcessDocQA(sp,mcfg,ch))
                model,Cmd.none
            | None -> model,Cmd.ofMsg(ShowInfo "Service configuration not yet received from server")
        else 
            model,Cmd.ofMsg(ShowInfo "Question is empty")

    let genSearch serverDispatch id model =          
        match model.serviceParameters with
        | Some sp -> 
            let chats = 
                model.interactions
                |> Interactions.setDocumentStatus id ExtractingTerms
            let model = {model with interactions = chats; error=None}
            let ch = 
                model.interactions 
                |> List.find(fun x->x.Id=id) 
                |> Interaction.removeUIState
            match ch.InteractionType with
            | DocQA _ -> serverDispatch (Clnt_SearchQuery(sp,model.appConfig.ModelsConfig,ch))
            | _       -> failwith "unexptected chat type"
            model,Cmd.none
        | None -> model,Cmd.ofMsg(ShowInfo "Service configuration not yet received from server")

    let completeChat id err model =
        let cs = Interactions.endBuffering id (Option.isSome err) model.interactions
        let model = {model with interactions = cs}
        let cmd = err |> Option.map(fun e -> Cmd.ofMsg(ShowError e)) |> Option.defaultValue Cmd.none
        model,cmd
        
    let checkAddInteraction ctype model =
        let backend = Init.defaultBackend model
        if model.interactions.Length < C.MAX_INTERACTIONS then
            let id,cs = Interactions.addNew backend ctype None model.interactions      
            let c = cs |> List.find (fun c -> c.Id=id)
            let sParms = model.serviceParameters
            let cs = 
                cs
                |> Interactions.setSystemMessage id model.appConfig.DefaultSystemMessage
                |> Interactions.setMaxDocs id model.appConfig.DefaultMaxDocs
            {model with interactions = cs; selectedChatId = Some id },Cmd.none
        else
            model,Cmd.ofMsg (ShowInfo "Max number of tabs reached")

    let updateDocs (id,docs) model = 
        {model with interactions = Interactions.addDocuments id docs model.interactions}

    let updateSearchTerms (id,srchQ) model =
        model.interactions
        |> List.tryFind(fun c -> c.Id = id)
        |> Option.map Interaction.docBag 
        |> Option.map(fun bag -> Interactions.setDocBag id {bag with SearchTerms = Some srchQ;} model.interactions)
        |> Option.map(fun cs -> Interactions.setDocumentStatus id Ready cs)
        |> Option.map(fun cs -> {model with interactions = cs})
        |> Option.defaultValue model

    let removeChat id model = 
        {model with 
            interactions = model.interactions |> List.filter (fun c -> c.Id <> id)
            tempChatSettings = model.tempChatSettings |> Map.remove id
        }

    let tryApplyTemplate (id,tpType,template) model =
        try 
            let ixs = Interactions.applyTemplate id (tpType,template) model.interactions
            {model with interactions = ixs},Cmd.none
        with ex ->
            model,Cmd.ofMsg (ShowInfo ex.Message)

    let postLoaded savedChats model = 
        let model = 
            if List.isEmpty savedChats then 
                model                                       //no saved chats found, keep samples
            else 
                {model with interactions = savedChats}      //replace samples with saved chats
        let msg = if savedChats.IsEmpty then "No saved chats. Showing samples" else "Loaded saved chats"   
        let cs = model.interactions
        let firstChat = cs |> List.tryHead
        let model = {model with selectedChatId = firstChat |> Option.map (fun c -> c.Id)}
        model,Cmd.ofMsg (FlashInfo msg)

    let delaySubmit id =
        async {
            do! Async.Sleep 500
            return (id,false)
        }

    let submitOnKey model id delay =
        if delay then 
            model, Cmd.OfAsync.perform delaySubmit id Ia_SubmitOnKey
        else 
            let msg = Ia_Submit(id,(selectedChat model) |> Option.map (_.Question) |> Option.defaultValue "")
            model,Cmd.ofMsg msg        

    let flashMessage uparms model msg = 
        let model = 
            if model.flashBanner && model.appConfig.PersonaText.IsSome then 
                Init.flashBanner uparms model msg
                {model with flashBanner = false}
            else
                uparms.snkbar.Add(
                        msg,
                        configure = fun o ->
                            o.VisibleStateDuration<-1000
                            o.HideTransitionDuration<-300
                        ) |> ignore
                model
        model,Cmd.none

    //if there is an exception when processing a message, the Elmish message loop terminates
    let update (uparms:UpdateParms) message model =
        //printfn "%A" message
        match message with
        | StartInit -> Init.pingServer uparms.serverDispatch; {model with busy=true},Cmd.ofMsg LoadUIState

        //interactions
        | Ia_Submit (id,lastMsg) -> checkBusy model <| Auth.checkAuthFlip (submitChat uparms.serverDispatch lastMsg id)
        | Ia_SubmitOnKey (id,delay) -> submitOnKey model id delay
        | Ia_SystemMessage (id,msg) -> {model with interactions = Interactions.setSystemMessage id msg model.interactions},Cmd.none
        | Ia_ApplyTemplate (id,tpType,tmplt) -> tryApplyTemplate (id,tpType,tmplt) model
        | Ia_SetPrompt (id,tpType,prompt) -> {model with interactions = Interactions.setPrompt id (tpType,prompt) model.interactions}, Cmd.none
        | Ia_Save -> model, Cmd.OfTask.either IO.saveChats (model,uparms.localStore) ShowInfo Error
        | Ia_LoadChats -> model, Cmd.OfTask.either IO.loadChats uparms.localStore Ia_LoadedChats Error
        | Ia_LoadedChats cs -> postLoaded cs model
        | Ia_ClearChat (id,prompt) -> {model with interactions = Interactions.clearChat id prompt model.interactions},Cmd.none
        | Ia_ClearChats -> {model with interactions=[]},Cmd.ofMsg(ShowInfo "Chats cleared")
        | Ia_DeleteSavedChats -> model,Cmd.OfTask.either IO.deleteSavedChats uparms.localStore ShowInfo Error
        | Ia_AddMsg (id,msg) -> {model with interactions = Interactions.addMessage id msg model.interactions},Cmd.none
        | Ia_SetQuestion (id,prompt) -> {model with interactions = Interactions.setQuestion id prompt model.interactions},Cmd.none
        | Ia_Restart (id,msg) -> {model with interactions = model.interactions |> Interactions.restartFromMsg id msg},Cmd.none
        | Ia_UpdateName (id,n) -> {model with interactions = Interactions.setName id (Some n) model.interactions},Cmd.none
        | Ia_UpdateParms (id,p) -> {model with interactions = Interactions.setParms id p model.interactions},Cmd.none
        | Ia_AddDelta (id,delta) -> {model with interactions = Interactions.addDelta id delta model.interactions},Cmd.none
        | Ia_Completed(id,err) -> completeChat id err model
        | Ia_Add ctype -> checkAddInteraction ctype model
        | Ia_Notification (id,note) -> {model with interactions = Interactions.addNotification id note model.interactions},Cmd.none
        | Ia_UpdateQaBag (id,bag) -> {model with interactions = Interactions.setQABag id bag model.interactions},Cmd.none
        | Ia_UpdateDocBag (id,dbag) -> {model with interactions = Interactions.setDocBag id dbag model.interactions},Cmd.none
        | Ia_File_BeingLoad (id,dbag) -> {model with interactions = Interactions.setDocBag id dbag model.interactions},Cmd.ofMsg (Ia_File_Load id)
        | Ia_File_Load id -> {model with interactions = Interactions.setDocumentStatus id Uploading model.interactions},Cmd.OfTask.either IO.loadFile (id,model,uparms.serverCall) Ia_File_Loaded Error
        | Ia_File_Loaded (id,fileId) -> uparms.serverDispatch (Clnt_ExtractContents (id,fileId)); {model with interactions = Interactions.setDocumentStatus id Receiving model.interactions},Cmd.none
        | Ia_File_SetContents (id,txt,isDone) -> {model with interactions = Interactions.setFileContents id (txt,isDone) model.interactions},if isDone then Cmd.ofMsg(Ia_GenSearch(id)) else Cmd.none
        | Ia_GenSearch id -> checkBusy model <| (genSearch uparms.serverDispatch id)
        | Ia_SetSearch(id,txt) -> updateSearchTerms (id,txt) model,Cmd.none
        | Ia_Remove id -> removeChat id model,Cmd.none
        | Ia_Selected id -> {model with selectedChatId = Some id},Cmd.none
        | Ia_UseWeb (id,useWeb) -> {model with interactions = Interactions.setUseWeb id useWeb model.interactions},Cmd.none
        | Ia_SetIndex (id,idxs) -> {TmpState.toggleIndex id model  with interactions = Interactions.setIndexes id idxs model.interactions},Cmd.none
        | Ia_ToggleDocOnly id -> {model with interactions = Interactions.toggleDocOnly id model.interactions},Cmd.none
        | Ia_ToggleSettings id -> TmpState.toggleChatSettings id model,Cmd.none
        | Ia_ToggleDocs (id,msgId) -> TmpState.toggleChatDocs (id,msgId) model, Cmd.none
        | Ia_ToggleDocDetails id -> TmpState.toggleDocDetails id model, Cmd.none
        | Ia_TogglePrompts id -> TmpState.togglePrompts id model, Cmd.none
        | Ia_OpenIndex id -> TmpState.toggleIndex id model, Cmd.none
        | Ia_ToggleSysMsg id -> TmpState.toggleSysMsg id model, Cmd.none

        //session and state
        | Clear -> checkBusy model <| fun _-> {model with interactions=Interactions.empty},Cmd.none
        | Error exn -> model,Cmd.ofMsg (ShowError exn.Message)
        | ShowError str -> uparms.snkbar.Add(str,severity=Severity.Error) |> ignore;model,Cmd.none
        | ShowInfo str -> uparms.snkbar.Add(str) |> ignore; model,Cmd.none
        | FlashInfo str -> flashMessage uparms model str
        | Nop () -> model,Cmd.none
        | ClearError -> {model with error = None},Cmd.none
        | Reset        -> checkBusy model <| fun _ -> {model with interactions=Interactions.empty},Cmd.none
        | OpenCloseSettings id -> TmpState.openClose id model, Cmd.none
        | RefreshIndexes initial -> checkBusy model <| IO.refreshIndexes uparms.serverDispatch initial
        | GetOpenAIKey -> IO.getKeyFromLocal uparms.localStore model
        | SetOpenAIKey key -> {model with serviceParameters = model.serviceParameters |> Option.map (fun p -> {p with OPENAI_KEY = Some key})},Cmd.none
        | UpdateOpenKey key -> model,Cmd.batch [Cmd.ofMsg (SetOpenAIKey key); Cmd.ofMsg (SaveToLocal(C.LS_OPENAI_KEY,key))]
        | SaveToLocal (k,v) -> uparms.localStore.SetItemAsync(k,v) |> ignore; model,Cmd.none
        | ToggleTheme -> {model with darkTheme = not model.darkTheme},Cmd.ofMsg SaveUIState
        | ToggleTabs -> {model with tabsUp = not model.tabsUp},Cmd.ofMsg SaveUIState
        | SaveUIState -> model, Cmd.batch [Cmd.ofMsg (SaveToLocal(C.DARK_THEME,model.darkTheme)); Cmd.ofMsg(SaveToLocal(C.TABS_UP,model.tabsUp))]
        | LoadUIState -> model,Cmd.OfTask.either IO.loadUIState uparms.localStore LoadedUIState Error
        | LoadedUIState (darkTheme,tabsUp) -> {model with darkTheme = darkTheme; tabsUp = tabsUp},Cmd.none
        | IgnoreError ex -> model,Cmd.none
        | PurgeLocalData -> model, Cmd.OfTask.either IO.purgeLocalStorage uparms.localStore ShowInfo Error
        //authentication
        | SetPage page -> { model with page = page }, Cmd.none
        | LoginLogout -> Auth.loginLogout uparms.navMgr model;model,Cmd.none
        | SetAuth user -> Auth.postAuth model user
        | GetUserDetails -> model, Cmd.OfTask.either Graph.Api.getDetails (model.user,uparms.httpFac) GotUserDetails Error
        | GotUserDetails data -> {model with photo=data},Cmd.none

        //server initiated
        | FromServer (Srv_Parameters p) -> {model with serviceParameters=Some p;}, Cmd.none
        | FromServer (Srv_Ia_Delta(id,i,d)) -> model,Cmd.ofMsg(Ia_AddDelta(id,d))
        | FromServer (Srv_Ia_Done(id,err)) -> model, Cmd.ofMsg(Ia_Completed(id,err))
        | FromServer (Srv_Error err) -> model,Cmd.ofMsg (ShowError err)
        | FromServer (Srv_Info err) -> model,Cmd.ofMsg (ShowInfo err)
        | FromServer (Srv_IndexesRefreshed idxTrs) -> {model with busy=false; indexTrees=idxTrs},Cmd.none
        | FromServer (Srv_Ia_Notification (id,note)) -> model,Cmd.ofMsg(Ia_Notification(id,note))
        | FromServer (Srv_Ia_SetDocs (id,docs)) -> updateDocs (id,docs) model, Cmd.none
        | FromServer (Srv_Ia_SetContents (id,cntnt,isDone)) -> model,Cmd.ofMsg(Ia_File_SetContents(id,cntnt,isDone))
        | FromServer (Srv_Ia_SetSearch (id,query)) -> model,Cmd.ofMsg(Ia_SetSearch(id,query))
        | FromServer (Srv_SetTemplates templates) -> {model with templates = templates},Cmd.none
        | FromServer (Srv_LoadSamples (lbl,samples)) -> Init.addSamples lbl samples model
        | FromServer (Srv_SetConfig appConfig) -> {model with appConfig=appConfig; theme=AppConfig.toTheme appConfig},Cmd.none
        | FromServer (Srv_DoneInit _) -> Init.postServerInit model
