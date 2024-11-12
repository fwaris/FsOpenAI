﻿namespace FsOpenAI.Client
open System
open Elmish
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open Radzen

module Update =

    let handleError (exn:Exception) model =
        printfn $"Error: {exn}"
        match exn with 
//      | <handle typed exceptions>
        | _ -> model, Cmd.ofMsg (ShowError exn.Message)

    //if there is an exception when processing a message, the Elmish message loop terminates
    let update (uparms:UpdateParms) message model =
        //printfn "%A" message
        match message with
        | StartInit -> Init.pingServer uparms.serverDispatchUnAuth; {model with busy=true},Cmd.ofMsg LoadUIState

        (*
            Initalization Flow:
                Client   <---------->  Server
                Clnt_Connected --->
                                <--- Srv_SetConfig
                                <--- Srv_Parameters
                                <--- Srv_IndexesRefreshed
                                <--- Srv_SetTemplates
                                <--- Srv_LoadSamples
                                <--- Srv_DoneInit
        *)

        //interactions
        | Ia_Submit (id,lastMsg) -> Model.checkBusy model <| Auth.checkAuthFlip (Submission.submitChat uparms.serverDispatch lastMsg id)
        | Ia_SubmitOnKey (id,delay) -> Submission.submitOnKey model id delay
        | Ia_SystemMessage (id,msg) -> {model with interactions = Interactions.setSystemMessage id msg model.interactions},Cmd.none
        | Ia_ApplyTemplate (id,tpType,tmplt) -> Submission.tryApplyTemplate (id,tpType,tmplt) model
        | Ia_SetPrompt (id,tpType,prompt) ->  printfn "TODO set prompt"; model,Cmd.none //{model with interactions = Interactions.setPrompt id (tpType,prompt) model.interactions}, Cmd.none
        | Ia_Save id -> model, if Model.isChatPeristenceConfigured model then Cmd.ofMsg (Ia_Session_Save id) else Cmd.ofMsg Ia_Local_Save
        | Ia_Local_Save -> model, Cmd.OfTask.either IO.saveChats (model,uparms.localStore) ShowInfo Error
        | Ia_Local_Load -> model, Cmd.OfTask.either IO.loadChats uparms.localStore Ia_Local_Loaded Error
        | Ia_Local_Loaded cs -> Submission.tryLoadSamples model
        | Ia_Local_ClearAll -> model,Cmd.OfTask.either IO.deleteSavedChats uparms.localStore ShowInfo Error
        | Ia_Session_Load -> uparms.serverDispatch (Clnt_Ia_Session_LoadAll (IO.invocationContext model)); model,Cmd.none
        | Ia_Session_Save id -> Submission.saveSession uparms.serverDispatch id model
        | Ia_Session_Delete id -> Submission.sessionDelete uparms.serverDispatch id model
        | Ia_Session_ClearAll -> uparms.serverDispatch (Clnt_Ia_Session_ClearAll (IO.invocationContext model)); model,Cmd.none
        | Ia_ResetChat (id,prompt) -> {model with interactions = Interactions.clearChat id prompt model.interactions},Cmd.none
        | Ia_ClearChats -> Submission.clearChats model
        | Ia_AddMsg (id,msg) -> {model with interactions = Interactions.addMessage id msg model.interactions},Cmd.none
        | Ia_SetQuestion (id,prompt) -> {model with interactions = Interactions.setQuestion id prompt model.interactions},Cmd.ofMsg (Ia_Save id)
        | Ia_Restart (id,msg) -> {model with interactions = model.interactions |> Interactions.restartFromMsg id msg},Cmd.none
        | Ia_UpdateName (id,n) -> {model with interactions = Interactions.setName id (Some n) model.interactions},Cmd.none
        | Ia_UpdateParms (id,p) -> {model with interactions = Interactions.setParms id p model.interactions},Cmd.none
        | Ia_AddDelta (id,delta) -> {model with interactions = Interactions.addDelta id delta model.interactions},Cmd.none
        | Ia_Completed(id,err) -> Submission.completeChat id err model
        | Ia_Add ctype -> Submission.checkAddInteraction ctype model
        | Ia_Notification (id,note) -> {model with interactions = Interactions.addNotification id note model.interactions},Cmd.none
        | Ia_UpdateQaBag (id,bag) -> {model with interactions = Interactions.setQABag id bag model.interactions},Cmd.none
        | Ia_Feedback_Set (id,fb) -> {model with interactions = Interactions.setFeedback id (Some fb) model.interactions},Cmd.none
        | Ia_SetSearch(id,txt) -> Submission.updateSearchTerms (id,txt) model,Cmd.none
        | Ia_Remove id -> Submission.removeChat id model
        | Ia_Selected id -> {model with selectedChatId = Some id},Cmd.none
        | Ia_UseWeb (id,useWeb) -> Submission.setModeUseWeb useWeb id model,Cmd.none
        | Ia_SetIndex (id,idxs) -> Submission.setModeIndexes idxs id model,Cmd.none
        | Ia_Remove_Document id -> Submission.removeDoc id model,Cmd.none
        | Ia_Mode_Document id -> {model with interactions = Interactions.setMode id M_Doc model.interactions},Cmd.none
        | Ia_Mode_Doc_Index (id,useIndex) -> Submission.setModeDocIndex useIndex id model,Cmd.none
        | Ia_File_BeingLoad2 (id,dc) -> Submission.setModeDoc dc id model, Cmd.ofMsg (Ia_File_Load id)
        | Ia_File_Load id -> {model with interactions = Interactions.setDocumentStatus id Uploading model.interactions},Cmd.OfTask.either IO.loadFile (id,model,uparms.serverCall) Ia_File_Loaded Error
        | Ia_File_Loaded (id,fileId) -> Submission.extractContents  uparms.serverDispatch id fileId model
        | Ia_File_SetContents (id,txt,isDone) -> {model with interactions = Interactions.setFileContents id (txt,isDone) model.interactions},Cmd.none
        | Ia_ToggleSettings id -> TmpState.toggleChatSettings id model,Cmd.none
        | Ia_ToggleDocs (id,msgId) -> TmpState.toggleChatDocs (id,msgId) model, Cmd.none
        | Ia_ToggleDocDetails id -> TmpState.toggleDocDetails id model, Cmd.none
        | Ia_TogglePrompts id -> TmpState.togglePrompts id model, Cmd.none
        | Ia_OpenIndex id -> TmpState.toggleIndex id model, Cmd.none
        | Ia_ToggleSysMsg id -> TmpState.toggleSysMsg id model, Cmd.none
        | Ia_ToggleFeedback(id) -> TmpState.toggleFeedback id model, Cmd.none
        | Ia_Feedback_Submit id -> Submission.submitFeedback uparms.serverDispatch id model; model,Cmd.none
        //session and state
        | CloseBanner _ -> uparms.dialogService.Close(); model, if model.appConfig.RequireLogin then Cmd.ofMsg (ShowInfo "Please login to continue") else Cmd.none
        | Error exn -> handleError exn model
        | ShowError str -> uparms.notificationService.Notify(detail=str, severity=NotificationSeverity.Error, duration=3000.) |> ignore; model,Cmd.none
        | ShowInfo str -> uparms.notificationService.Notify(detail=str) |> ignore; model,Cmd.none
        | FlashBanner -> Init.checkStartBanner uparms model
        | Nop () -> model,Cmd.none
        | ClearError -> {model with error = None},Cmd.none
        | ToggleSideBar -> TmpState.toggle C.SIDE_BAR_EXPANDED model,Cmd.none
        | SidebarExpanded isExpanded -> TmpState.setState C.SIDE_BAR_EXPANDED isExpanded model,Cmd.none
        | OpenCloseSettings id -> TmpState.openClose id model, Cmd.none
        | RefreshIndexes initial -> Model.checkBusy model <| IO.refreshIndexes uparms.serverDispatch initial
        | GetOpenAIKey -> IO.getKeyFromLocal uparms.localStore model
        | SetOpenAIKey key -> {model with serviceParameters = model.serviceParameters |> Option.map (fun p -> {p with OPENAI_KEY = Some key})},Cmd.none
        | UpdateOpenKey key -> model,Cmd.batch [Cmd.ofMsg (SetOpenAIKey key); Cmd.ofMsg (SaveToLocal(C.LS_OPENAI_KEY,key))]
        | SaveToLocal (k,v) -> uparms.localStore.SetItemAsync(k,v) |> ignore; model,Cmd.none
        | SaveUIState -> model, Cmd.batch [Cmd.ofMsg (SaveToLocal(C.DARK_THEME,model.darkTheme));]
        | LoadUIState -> model,Cmd.OfTask.either IO.loadUIState uparms.localStore LoadedUIState Error
        | LoadedUIState (darkTheme) -> {model with darkTheme = darkTheme},Cmd.none
        | IgnoreError ex -> model,Cmd.none
        | PurgeLocalData -> model, Cmd.OfTask.either IO.purgeLocalStorage uparms.localStore ShowInfo Error

        //authentication
        | SetPage page -> { model with page = page }, Cmd.none
        | LoginLogout -> Auth.loginLogout uparms.navMgr model;model,Cmd.none
        | SetAuth user -> Auth.postAuth model user
        | GetUserDetails -> model, Cmd.OfTask.either Graph.Api.getDetails (model.user,uparms.httpFac) GotUserDetails Error
        | GotUserDetails data -> {model with photo=data},Cmd.none

        //server initiated
        | FromServer (Srv_DoneInit _) -> Init.postServerInit model
        | FromServer (Srv_SetConfig appConfig) -> {model with appConfig=appConfig},Cmd.ofMsg FlashBanner
        | FromServer (Srv_IndexesRefreshed idxTrs) -> {model with busy=false; indexTrees=idxTrs},Cmd.none
        | FromServer (Srv_Parameters p) -> {model with serviceParameters=Some p;}, Cmd.none
        | FromServer (Srv_SetTemplates templates) -> {model with templates = templates},Cmd.none
        | FromServer (Srv_LoadSamples (lbl,samples)) -> {model with samples = (lbl,samples)::model.samples}, Cmd.none
        | FromServer (Srv_Ia_Reset id) -> {model with interactions = Interactions.resetChat id model.interactions},Cmd.none
        | FromServer (Srv_Ia_Citations (id,cits)) -> {model with interactions = Interactions.setCitations id cits model.interactions},Cmd.none
        | FromServer (Srv_Ia_Delta(id,d)) -> model,Cmd.ofMsg(Ia_AddDelta(id,d))
        | FromServer (Srv_Ia_Done(id,err)) -> model, Cmd.ofMsg(Ia_Completed(id,err))
        | FromServer (Srv_Error err) -> model,Cmd.ofMsg (ShowError err)
        | FromServer (Srv_Info err) -> model,Cmd.ofMsg (ShowInfo err)
        | FromServer (Srv_Ia_Notification (id,note)) -> model,Cmd.ofMsg(Ia_Notification(id,note))
        | FromServer (Srv_Ia_SetDocs (id,docs)) -> Submission.updateDocs (id,docs) model, Cmd.none
        | FromServer (Srv_Ia_File_Chunk (id,cntnt,isDone)) -> model,Cmd.ofMsg(Ia_File_SetContents(id,cntnt,isDone))
        | FromServer (Srv_Ia_File_Error (id,err)) -> {model with interactions = Interactions.clearDocContent id model.interactions},Cmd.ofMsg(ShowError err)
        | FromServer (Srv_Ia_SetSearch (id,query)) -> model,Cmd.ofMsg(Ia_SetSearch(id,query))
        | FromServer (Srv_Ia_Session_Loaded ch) -> {model with interactions = IO.fixIndexRefs model [ch] @ model.interactions},Cmd.none
        | FromServer (Srv_Ia_Session_DoneLoading) -> Submission.tryLoadSamples model
        | FromServer (Srv_Ia_SetSubmissionId(id,logId)) -> model,Cmd.ofMsg(Ia_Feedback_Set(id,Feedback.Default logId))
        //code eval
        | FromServer(Srv_Ia_SetCode(id,c)) -> {model with interactions = CodeEval.Interactions.setCode id c model.interactions},Cmd.none
        | FromServer(Srv_Ia_SetPlan(id,p)) -> {model with interactions = CodeEval.Interactions.setPlan id p model.interactions},Cmd.none


