namespace FsOpenAI.Client
open System.IO
open System.Threading.Tasks
open FsOpenAI.Shared
open FsOpenAI.Client
open FsOpenAI.Shared.Interactions
open Blazored.LocalStorage
open Elmish

//manage various IO operations required (e.g. local storage, server calls to get indexes, etc.)
module IO =
    open Microsoft.AspNetCore.Components.Forms

    let browserFile (o:obj option) = o |> Option.map(fun f -> (box f) :?> IBrowserFile)

    let docType (path:string) =
        let ext = Path.GetExtension(path).ToLowerInvariant()
        printfn $"document ext: {ext}"
        let docType =
            match ext with
            | ".txt" | ".text" -> Some DT_Text
            | ".pdf"           -> Some DT_Pdf
            | ".docx" | ".doc" -> Some DT_Word
            | ".pptx" | ".ppt" -> Some DT_Powerpoint
            | ".xlsx" | ".xls" -> Some DT_Excel
            | ".rtf"           -> Some DT_RTF
            | _                -> None
        printfn $"document type: {docType}"
        docType

    let invocationContext model =
        // let userAgent =
        //     model.appConfig.AppId
        //     |> Option.map(fun a -> match model.user with Authenticated u-> $"{a}:{u.Name}" | _ -> a)
        {
            AppId = model.appConfig.AppId
            User = (match model.user with Authenticated u -> u.Email | _ -> "Unauthenticated") |> Some
            ModelsConfig = model.appConfig.ModelsConfig
        }

    //flat set of all nodes rooted at the given node
    let rec subTree acc (t:IndexTree) =
        let acc = Set.add t acc
        (acc,t.Children) ||> List.fold subTree

    ///try to map any tags in the index refs to the actual index names
    let remapIndexRefs treeMap (idxs:Set<IndexRef>) =
        idxs
        |> Set.map(fun idx ->
            match idx with
            | Azure _ ->
                treeMap
                |> Map.tryFind idx
                |> Option.bind (fun t -> t.IndexName)
                |> Option.map(fun n -> Azure n)
                |> Option.defaultValue idx
            |  x -> failwith $"Index of type {x} not handled")

    let expandIdxRefs model (idxs:IndexRef list) =
        let treeMap = Init.flatten model.indexTrees |> List.map(fun x -> x.Idx,x) |> Map.ofList
        let rec loop acc (idx: IndexRef) =
            if idx.isVirtual then               //if index is virtual then loop over its children to add non-virtual parents to the set
                let subT = subTree Set.empty treeMap.[idx] |> Set.map(_.Idx)
                let children = Set.remove idx subT
                (acc,children) ||> Set.fold loop
            else
                acc |> Set.add idx              //if index is not virtual then don't include children. assume index contains the contents of all children also
        (Set.empty,idxs)
        ||> List.fold loop
        |> remapIndexRefs treeMap

    let refreshIndexes serverDispatch initial model  =
        match model.appConfig.MetaIndex with
        | Some metaIndex ->
            let msgf sp = Clnt_RefreshIndexes (sp,initial,model.appConfig.IndexGroups,metaIndex)
            match model.serviceParameters with
            | Some sp -> serverDispatch (msgf sp); {model with busy=true},Cmd.none
            | None    -> model,Cmd.ofMsg(ShowInfo "Service Parameters missing")
        | None -> model,Cmd.none

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

    let loadChatSessions serverDispatch model =
        let invCtx = invocationContext model
        serverDispatch (Clnt_Ia_Session_LoadAll invCtx)

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
            let docCntnt = Interaction.docContent ch
            let doc = browserFile docCntnt.DocumentRef
            let file = match doc with
                       | Some f -> f
                       | None   -> failwith "select file no accessible"
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
