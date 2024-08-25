namespace FsOpenAI.Client
open FsOpenAI.Shared

//manage temporary state for chat UI settings
module TmpState = 
    let isOpenDef defVal key model = 
        model.settingsOpen 
        |> Map.tryFind key 
        |> Option.defaultValue defVal

    let isOpen key model = isOpenDef false key model

    let toggle key model = 
        {model with 
            settingsOpen = 
                model.settingsOpen 
                |> Map.tryFind key 
                |> Option.map(fun b -> model.settingsOpen |> Map.add key (not b))
                |> Option.defaultWith(fun _ -> model.settingsOpen |> Map.add key true)}

    let setState key value model = 
        {model with settingsOpen = model.settingsOpen |> Map.add key value}    

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

    let toggleFeedback id model =
        updateChatTempState id model 
            (fun s -> {s with FeedbackOpen = not s.FeedbackOpen}) 
            {TempChatState.Default with FeedbackOpen=true}

    let isDocsOpen model = 
        let selChat = Model.selectedChat model
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

    let isFeedbackOpen id model =
        model.tempChatSettings
        |> Map.tryFind id
        |> Option.map(fun x -> x.FeedbackOpen)
        |> Option.defaultValue false
