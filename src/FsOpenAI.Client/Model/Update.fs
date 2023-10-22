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

type UpdateParms = 
    {
        localStore          : ILocalStorageService
        snkbar              : ISnackbar
        navMgr              : NavigationManager
        httpFac             : IHttpClientFactory
        serverDispatch      : ClientInitiatedMessages -> unit
        serverCall          : ClientInitiatedMessages -> Task            
    }

module Update =
        
    let initModel =    
        {
            interactions = []
            templates = []
            appConfig = AppConfig.Default
            interactionCreateTypes = newInteractionTypes
            indexRefs = []
            error = None
            busy = false
            settingsOpen = Map.empty 
            serviceParameters = None   
            darkTheme = true
            theme = new MudBlazor.MudTheme()
            user = Unauthenticated
            page = Home
            photo = None
            selected = None
        }

    let checkBusy model apply = 
        if model.busy then 
            model,Cmd.none
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
                let ch = 
                    model.interactions 
                    |> List.find(fun x->x.Id=id) 
                    |> Interaction.preSubmit
                match ch.InteractionType with
                | QA _   -> serverDispatch (Clnt_ProcessQA(sp,ch))
                | Chat _ -> serverDispatch (Clnt_ProcessChat(sp,ch))
                | DocQA _ -> serverDispatch (Clnt_ProcessDocQA(sp,ch))
                model,Cmd.none
            | None -> model,Cmd.ofMsg(ShowInfo "Service configuration not yet received from server")
        else 
            model,Cmd.ofMsg(ShowInfo "Question is empty")

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

    let genSearch serverDispatch id model =          
        match model.serviceParameters with
        | Some sp -> 
            let chats = 
                model.interactions
                |> Interactions.setDocumentStatus id GenSearch
            let model = {model with interactions = chats; error=None}
            let ch = 
                model.interactions 
                |> List.find(fun x->x.Id=id) 
                |> Interaction.preSubmit
            match ch.InteractionType with
            | DocQA _ -> serverDispatch (Clnt_SearchQuery(sp,ch))
            | _       -> failwith "unexptected chat type"
            model,Cmd.none
        | None -> model,Cmd.ofMsg(ShowInfo "Service configuration not yet received from server")

    let completeChat id err model =
        let cs = Interactions.endBuffering id (Option.isSome err) model.interactions
        let model = {model with interactions = cs}
        let cmd = err |> Option.map(fun e -> Cmd.ofMsg(ShowError e)) |> Option.defaultValue Cmd.none
        model,cmd
      

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
            let cs = Interactions.setParms (id,cParms) cs
            {model with interactions = cs; selected=Some id},Cmd.none
        else
            model,Cmd.ofMsg (ShowInfo "Max number of tabs reached")

    let refreshIndexes serverDispath initial model  =        
        match model.serviceParameters with
        | Some sp -> serverDispath (Clnt_RefreshIndexes (sp,initial,model.appConfig.IndexGroups)); {model with busy=true},Cmd.none
        | None    -> model,Cmd.ofMsg(ShowInfo "Service Parameters missing")

    let openClose id model =  
        {model with 
            settingsOpen = 
                model.settingsOpen 
                |> Map.tryFind id 
                |> Option.map(fun b -> model.settingsOpen |> Map.add id (not b))
                |> Option.defaultWith(fun _ -> model.settingsOpen |> Map.add id true)
        }

    let isOpen id model = model.settingsOpen |> Map.tryFind id |> Option.defaultValue false

    let getKeyFromLocal (localStore:ILocalStorageService) model =
        match model.serviceParameters with 
        | Some p when p.OPENAI_KEY.IsNone || Utils.isEmpty p.OPENAI_KEY.Value ->
            let t() = task{return! localStore.GetItemAsync<string> C.LS_OPENAI_KEY}
            model,Cmd.OfTask.either t () SetOpenAIKey IgnoreError
        | _ -> model,Cmd.none

    let updateDocs (id,docs) model = 
        {model with interactions = Interactions.setDocuments id docs model.interactions}

    let updateSearchTerms (id,srchQ) model =
        model.interactions
        |> List.tryFind(fun c -> c.Id = id)
        |> Option.bind Interaction.qaBag 
        |> Option.map(fun bag -> Interactions.setQABag id {bag with SearchQuery = Some srchQ;} model.interactions)
        |> Option.map(fun cs -> Interactions.setDocumentStatus id Ready cs)
        |> Option.map(fun cs -> {model with interactions = cs})
        |> Option.defaultValue model

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

    let tryApplyTemplate (id,tpType,template) model =
        try 
            let ixs = Interactions.applyTemplate id (tpType,template) model.interactions
            {model with interactions = ixs},Cmd.none
        with ex ->
            model,Cmd.ofMsg (ShowInfo ex.Message)

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

    let indexDesc = function Azure d -> d.Description

    let postLoaded cs model = 
        let model = if List.isEmpty cs then model else {model with interactions = cs}
        let msg = if cs.IsEmpty then "No saved chats. Showing samples" else "Loaded saved chats"        
        model,Cmd.ofMsg (FlashInfo msg)

    //if there is an exception when processing a message, the Elmish message loop terminates
    let update (uparms:UpdateParms) message model =
        //printfn "%A" message
        match message with
        | Init -> pingServer uparms.serverDispatch; {model with busy=true},Cmd.none

        //interactions
        | Ia_SystemMessage (id,msg) -> {model with interactions = Interactions.setSystemMessage (id,msg) model.interactions},Cmd.none
        | Ia_ApplyTemplate (id,tpType,tmplt) -> tryApplyTemplate (id,tpType,tmplt) model
        | Ia_SetPrompt (id,tpType,prompt) -> {model with interactions = Interactions.setPrompt id (tpType,prompt) model.interactions}, Cmd.none
        | Ia_Save -> model, Cmd.OfTask.either saveChats (model,uparms.localStore) ShowInfo Error
        | Ia_LoadChats -> model, Cmd.OfTask.either loadChats uparms.localStore Ia_LoadedChats Error
        | Ia_LoadedChats cs -> postLoaded cs model
        | Ia_ClearChats -> {model with interactions=[]},Cmd.ofMsg(ShowInfo "Chats cleared")
        | Ia_DeleteSavedChats -> model,Cmd.OfTask.either deleteSavedChats uparms.localStore ShowInfo Error
        | Ia_AddMsg (id,msg) -> {model with interactions = Interactions.addMessage (id,msg) model.interactions},Cmd.none
        | Ia_UpdateLastMsg (id,msg) -> {model with interactions = Interactions.setLastUserMessage(id,msg) model.interactions},Cmd.none
        | Ia_DeleteMsg (id,msg) -> {model with interactions = model.interactions |> Interactions.tryDeleteMessage (id,msg)},Cmd.none
        | Ia_UpdateName (id,n) -> {model with interactions = Interactions.setName (id,Some n) model.interactions},Cmd.none
        | Ia_UpdateParms (id,p) -> {model with interactions = Interactions.setParms (id,p) model.interactions},Cmd.none
        | Ia_AddDelta (id,delta) -> {model with interactions = Interactions.addDelta id delta model.interactions},Cmd.none
        | Ia_Completed(id,err) -> completeChat id err model
        | Ia_Add ctype -> checkAddInteraction ctype model
        | Ia_Notification (id,note) -> {model with interactions = Interactions.addNotification id note model.interactions},Cmd.none
        | Ia_UpdateQaBag (id,bag) -> {model with interactions = Interactions.setQABag id bag model.interactions},Cmd.none
        | Ia_UpdateDocBag (id,dbag) -> {model with interactions = Interactions.setDocBag id dbag model.interactions},Cmd.none
        | Ia_File_BeingLoad (id,dbag) -> {model with interactions = Interactions.setDocBag id dbag model.interactions},Cmd.ofMsg (Ia_File_Load id)
        | Ia_File_Load id -> {model with interactions = Interactions.setDocumentStatus id Uploading model.interactions},Cmd.OfTask.either loadFile (id,model,uparms.serverCall) Ia_File_Loaded Error
        | Ia_File_Loaded (id,fileId) -> uparms.serverDispatch (Clnt_ExtractContents (id,fileId)); {model with interactions = Interactions.setDocumentStatus id Extracting model.interactions},Cmd.none
        | Ia_File_SetContents (id,txt,isDone) -> {model with interactions = Interactions.setFileContents id (txt,isDone) model.interactions},if isDone then Cmd.ofMsg(Ia_GenSearch(id)) else Cmd.none
        | Ia_GenSearch id -> checkBusy model <| (genSearch uparms.serverDispatch id)
        | Ia_SetSearch(id,txt) -> updateSearchTerms (id,txt) model,Cmd.none
        | Ia_Remove id -> {model with interactions = Interactions.remove id model.interactions; },Cmd.none
        | Ia_Selected int -> {model with selected = Some int},Cmd.none
        | Ia_Submit (id,lastMsg) -> checkBusy model <| checkAuthFlip (submitChat uparms.serverDispatch lastMsg id)
        | Ia_UseWeb (id,useWeb) -> {model with interactions = Interactions.setUseWeb id useWeb model.interactions},Cmd.none

        //session and state
        | Clear -> checkBusy model <| fun _-> {model with interactions=Interactions.empty},Cmd.none
        | Error exn -> model,Cmd.ofMsg (ShowError exn.Message)
        | ShowError str -> uparms.snkbar.Add(str,severity=Severity.Error) |> ignore;model,Cmd.none
        | ShowInfo str -> uparms.snkbar.Add(str) |> ignore; model,Cmd.none
        | FlashInfo str -> uparms.snkbar.Add(str,configure=fun o->o.VisibleStateDuration<-1000; o.HideTransitionDuration<-300) |> ignore; model,Cmd.none
        | Nop () -> model,Cmd.none
        | ClearError -> {model with error = None},Cmd.none
        | Reset        -> checkBusy model <| fun _ -> {model with interactions=Interactions.empty},Cmd.none
        | OpenCloseSettings id -> openClose id model, Cmd.none
        | RefreshIndexes initial -> checkBusy model <| refreshIndexes uparms.serverDispatch initial
        | GetOpenAIKey -> getKeyFromLocal uparms.localStore model
        | SetOpenAIKey key -> {model with serviceParameters = model.serviceParameters |> Option.map (fun p -> {p with OPENAI_KEY = Some key})},Cmd.none
        | UpdateOpenKey key -> model,Cmd.batch [Cmd.ofMsg (SetOpenAIKey key); Cmd.ofMsg (SaveToLocal(C.LS_OPENAI_KEY,key))]
        | SaveToLocal (k,v) -> uparms.localStore.SetItemAsStringAsync(k,v) |> ignore; model,Cmd.none
        | ToggleTheme -> {model with darkTheme = not model.darkTheme},Cmd.none
        | IgnoreError ex -> model,Cmd.none

        //authentication
        | SetPage page -> { model with page = page }, Cmd.none
        | LoginLogout -> loginLogout uparms.navMgr model;model,Cmd.none
        | SetAuth user -> postAuth model user
        | GetUserDetails -> model, Cmd.OfTask.either Graph.Api.getDetails (model.user,uparms.httpFac) GotUserDetails Error
        | GotUserDetails data -> {model with photo=data},Cmd.none

        //server initiated
        | FromServer (Srv_Parameters p) -> {model with serviceParameters=Some p;}, Cmd.none
        | FromServer (Srv_Ia_Delta(id,i,d)) -> model,Cmd.ofMsg(Ia_AddDelta(id,d))
        | FromServer (Srv_Ia_Done(id,err)) -> model, Cmd.ofMsg(Ia_Completed(id,err))
        | FromServer (Srv_Error err) -> model,Cmd.ofMsg (ShowError err)
        | FromServer (Srv_Info err) -> model,Cmd.ofMsg (ShowInfo err)
        | FromServer (Srv_IndexesRefreshed idxs) -> {model with busy=false; indexRefs=idxs |> List.sortBy indexDesc},Cmd.none
        | FromServer (Srv_Ia_Notification (id,note)) -> model,Cmd.ofMsg(Ia_Notification(id,note))
        | FromServer (Srv_Ia_SetDocs (id,docs)) -> updateDocs (id,docs) model, Cmd.none
        | FromServer (Srv_Ia_SetContents (id,cntnt,isDone)) -> model,Cmd.ofMsg(Ia_File_SetContents(id,cntnt,isDone))
        | FromServer (Srv_Ia_SetSearch (id,query)) -> model,Cmd.ofMsg(Ia_SetSearch(id,query))
        | FromServer (Srv_SetTemplates templates) -> {model with templates = templates},Cmd.none
        | FromServer (Srv_LoadSamples (lbl,samples)) -> addSamples lbl samples model
        | FromServer (Srv_SetConfig appConfig) -> {model with appConfig=appConfig; theme=AppConfig.toTheme appConfig},Cmd.none
        | FromServer (Srv_DoneInit _) -> postServerInit model
