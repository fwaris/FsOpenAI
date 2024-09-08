namespace FsOpenAI.Client
open System
open Elmish
open FSharp.Control
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions.CodeEval
open FsOpenAI.Shared.Interactions
open FsOpenAI.Shared.Interactions.Core


//manage chat operations like submitting a chat, generating search, etc.
module Submission =

    let docType id cs = (Interactions.docContent id cs) |> Option.bind(fun d->d.DocType) 

    let setModeUseWeb useWeb id model =
        {model with 
            interactions =
                model.interactions
                |> Interactions.setUseWeb id useWeb
                |> Interactions.setMode id M_Plain}
                
    let setModeIndexes (idxs:IndexRef list) id model =
        match Model.selectedChat model, idxs.Length = 0 with
        | Some ch, true when ch.Mode <> M_Index -> model //no change if not in index mode and no indexes selected (handles Radzen idiosyncracy)
        | _ ->
            {model with 
                interactions = 
                            model.interactions
                            |> Interactions.setMode id M_Index 
                            |> Interactions.setIndexes id idxs}                        

    let setModeDoc (doc:DocumentContent) id model = 
        {model with interactions = 
                        model.interactions
                        |> Interactions.setDocContent id doc
                        |> Interactions.setMode id M_Doc}

    let setModeDocIndex useIndex id model = 
        if useIndex then
            {model with interactions = 
                            model.interactions
                            |> Interactions.setMode id M_Doc_Index}            
        else
            {model with interactions = 
                            model.interactions
                            |> Interactions.forceSetMode id M_Doc}

    let removeDoc id model =
        let defMode = 
            model.interactions |> List.tryFind (fun c -> c.Id = id)
            |> Option.map(fun c -> match c.Mode with M_Doc_Index -> M_Index | _ -> M_Plain)
        {model with interactions = 
                        model.interactions
                        |> Interactions.removeDoc id
                        |> Interactions.setMode id M_Plain}
            
    let isReady ch =
        match ch with 
        | Some ch -> 
            not ch.IsBuffering && 
                (Interaction.docContent ch
                |> Option.map(fun d -> d.Status = DocumentStatus.Ready)
                |> Option.defaultValue true)
        | None -> false

    let saveSession serviceDispatch id model =
        if Model.isChatPeristenceConfigured model then
            let ch = model.interactions |> List.find (fun c -> c.Id = id)
            let ch = ch |> Interaction.sessionSerialize
            serviceDispatch (Clnt_Ia_Session_Save (IO.invocationContext model,ch))
        model,Cmd.none

    let sessionDelete serviceDispatch id model =
        if Model.isChatPeristenceConfigured model then
            serviceDispatch (Clnt_Ia_Session_Delete (IO.invocationContext model,id))
        model,Cmd.none

    let clearChats model =
        let model = {model with interactions = []}
        let msg = Cmd.ofMsg (ShowInfo "Chats cleared")
        if Model.isChatPeristenceConfigured model then
            model,Cmd.batch [msg; Cmd.ofMsg Ia_Session_ClearAll]
        else
            model,Cmd.batch [msg; Cmd.ofMsg Ia_Local_ClearAll]

    let private sendChat model serverDispatch (ch:Interaction) =
        match ch.Mode with
        | M_Index   -> serverDispatch (Clnt_Run_IndexQnA(model.serviceParameters.Value,IO.invocationContext model,ch))
        | M_Plain -> serverDispatch (Clnt_Run_Plain(model.serviceParameters.Value,IO.invocationContext model,ch))
        | M_Doc -> serverDispatch (Clnt_Run_QnADoc(model.serviceParameters.Value,IO.invocationContext model,ch))
        | M_Doc_Index -> serverDispatch (Clnt_Run_IndexQnADoc(model.serviceParameters.Value,IO.invocationContext model,ch))
        | M_CodeEval -> serverDispatch (Clnt_Run_EvalCode(model.serviceParameters.Value,IO.invocationContext model,ch,(Interaction.code ch).CodeEvalParms))

    let checkSubmission prompt id model = 
        let ch = model.interactions |> List.tryFind (fun c -> c.Id = id)
        match ch, model.serviceParameters, Utils.isEmpty prompt with
        | None,_,_ -> Some "No chat selected"
        | _,None,_ -> Some "Service configuration not yet received from server"
        | _,_,true -> Some "Question is empty"
        | Some ch, _,_ when (ch.Mode = M_Index && (Interaction.getIndexes ch).Length = 0) -> Some "Please select a source"
        | _ -> None

    let prepForSubmit prompt id model =
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
            |> Interaction.setQuestion prompt //send the question to the server separately also for logging (note the prompt may be modfied along the way)
        let idxs = Interaction.getIndexes ch
        let ch =
            if List.isEmpty idxs then
                ch
            else
                ch |> Interaction.setIndexes (IO.expandIdxRefs model idxs |> Set.toList) //expand index list to include child indexes, if needed
        model,ch

    let submitChat serverDispatch prompt id model =
        match checkSubmission prompt id model with
        | Some error -> 
            let interactions = model.interactions |> Interactions.setQuestion id prompt
            let model' = {model with interactions=interactions}
            model',Cmd.ofMsg(ShowInfo error)
        | None ->
            let model,ch = prepForSubmit prompt id model
            sendChat model serverDispatch ch
            model,Cmd.none

    let completeChat id err model =
        let cs = Interactions.endBuffering id (Option.isSome err) model.interactions
        let model = {model with interactions = cs}
        let cmd =
            err
            |> Option.map(fun e -> Cmd.ofMsg(ShowError e))
            |> Option.defaultValue (Cmd.ofMsg (Ia_Save id))
        model,cmd

    let defSysMsg mode appConfg =
        appConfg.EnabledChatModes
        |> List.choose(fun (m,sysM) -> match m with mode -> Some sysM | _ -> None)
        |> List.tryHead
        |> Option.defaultValue C.defaultSystemMessage

    let checkAddInteraction ctype model =
        let backend = Init.defaultBackend model
        let next = Cmd.ofMsg (OpenCloseSettings C.ADD_CHAT_MENU)
        if model.interactions.Length < C.MAX_INTERACTIONS then
            let id,cs = Interactions.addNew backend ctype None model.interactions
            let c = cs |> List.find (fun c -> c.Id=id)
            let sParms = model.serviceParameters
            let cs =
                cs
                |> Interactions.setSystemMessage id (defSysMsg ctype model.appConfig)
                |> Interactions.setMaxDocs id model.appConfig.DefaultMaxDocs
            {model with interactions = cs; selectedChatId = Some id },next
        else
            model,Cmd.batch [next; Cmd.ofMsg (ShowInfo "Max number of tabs reached")]

    let updateDocs (id,docs) model =
        {model with interactions = Interactions.addDocuments id docs model.interactions}

    let updateSearchTerms (id,srchQ) model =
        model.interactions
        |> List.tryFind(fun c -> c.Id = id)
        |> Option.bind Interaction.docContent
        |> Option.map(fun d -> Interactions.setDocContent id {d with SearchTerms=Some srchQ} model.interactions)
        |> Option.map(fun cs -> Interactions.setDocumentStatus id Ready cs)
        |> Option.map(fun cs -> {model with interactions = cs})
        |> Option.defaultValue model

    let removeChat id model =
        let model =
            {model with
                interactions = model.interactions |> List.filter (fun c -> c.Id <> id)
                tempChatSettings = model.tempChatSettings |> Map.remove id
            }
        let cmd =
            if Model.isChatPeristenceConfigured model then
                Cmd.ofMsg (Ia_Session_Delete id)
            else
                Cmd.ofMsg (Ia_Local_Save)
        model,cmd

    
    let tryApplyTemplate (id,tpType,template) model =
        try
            let ixs = Interactions.applyTemplate id (tpType,template) model.interactions
            {model with interactions = ixs},Cmd.none
        with ex ->
            model,Cmd.ofMsg (ShowInfo ex.Message)

    let tryLoadSamples model =
        let model = {model with busy = false}
        try
            let msg = if model.interactions.IsEmpty then "No saved chats. Showing samples" else "Loaded saved chats"
            let interactions =
                if model.interactions.IsEmpty then
                    model.samples
                    |> List.collect (fun xs -> Init.createFromSamples xs model)
                else
                    model.interactions
            let model = {model with interactions = interactions}
            let cs = model.interactions
            let firstChat = cs |> List.tryHead
            let model = {model with selectedChatId = firstChat |> Option.map (fun c -> c.Id)}
            model,Cmd.ofMsg (FlashInfo msg)
        with ex ->
            model,Cmd.ofMsg (ShowError ex.Message)

    let delaySubmit id =
        async {
            do! Async.Sleep 500
            return (id,false)
        }

    let submitOnKey model id delay =
        if delay then
            model, Cmd.OfAsync.perform delaySubmit id Ia_SubmitOnKey
        else
            let msg = Ia_Submit(id,(Model.selectedChat model) |> Option.map (_.Question) |> Option.defaultValue "")
            model,Cmd.ofMsg msg

    let submitFeedback serverDispatch id model = 
        let ch = model.interactions |> List.find (fun c -> c.Id = id)
        match ch.Feedback with
        | Some fb -> serverDispatch (Clnt_Ia_Feedback_Submit(IO.invocationContext model,fb))
        | None -> ()

    
