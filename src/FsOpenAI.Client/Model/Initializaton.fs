namespace FsOpenAI.Client
open System
open Elmish
open FSharp.Control
open FsOpenAI.Client.Interactions
open Microsoft.AspNetCore.Components.Web

[<AutoOpen>]
module Init =
    open MudBlazor
    open Bolero.Html

    let private (===) (a:string) (b:string) = a.Equals(b,StringComparison.InvariantCultureIgnoreCase)
    let private updateBag bag ch = match bag with Some b -> Interaction.setQABag b ch | None -> ch
    let private updateIndx idxs ch = (ch,idxs) ||> List.fold (fun ch i -> Interaction.addIndex i ch) 
    let private setUseWeb useWeb ch = match ch.InteractionType with Chat cbag -> Interaction.setUseWeb useWeb ch | _ -> ch

    let newInteractionTypes = 
        [
            Icons.Material.Outlined.Chat, "New Chat", CreateChat AzureOpenAI
            Icons.Material.Outlined.QuestionAnswer, "New Index Q&A", CreateQA AzureOpenAI
            Icons.Material.Outlined.Chat, "New Chat", CreateChat OpenAI
            Icons.Material.Outlined.QuestionAnswer,"New Index Q&A", CreateQA OpenAI
        ]

    let backend (_,_,c)= match c with CreateChat bk -> bk | CreateQA bk -> bk | CreateDocQA (bk,_) -> bk


    let pingServer serverDispatch =
        async{
            do! Async.Sleep 100
            printfn "sending connect to server"
            serverDispatch (ClientInitiatedMessages.Clnt_Connected "me")
        }
        |> Async.StartImmediate

    let isAllowedSample appConfig ch =
        match appConfig.EnableOpenAI, ch.InteractionType, ch.Parameters.Backend with
        | _,Chat _, _  when not appConfig.EnableVanillaChat -> None
        | _,DocQA _, _ when not appConfig.EnableDocQuery    -> None
        | false,_,OpenAI                                    -> None
        | _                                                 -> Some ch

    let isAllowedCreate appConfig create =
        match appConfig.EnableOpenAI, create, backend create with
        | _,(_,_,CreateChat _),_ when not appConfig.EnableVanillaChat   -> None
        | _,(_,_,CreateDocQA _),_ when not appConfig.EnableDocQuery     -> None
        | false,_,OpenAI                                                -> None
        | _                                                             -> Some create

    let postServerInit model =
        let createTypes = model.interactionCreateTypes |> List.choose (isAllowedCreate model.appConfig)
        let bknds = createTypes |> List.map backend |> List.distinct
        let labels = model.templates |> List.map(fun x->x.Label)
        let docCreates =
            bknds
            |> List.collect (fun bk -> 
                labels 
                |> List.map(fun lbl -> 
                    let mText = $"New '{lbl}' Document Query"
                    Icons.Material.Outlined.Book, mText, CreateDocQA (bk,lbl)))
        let creates = newInteractionTypes @ docCreates
        {model with busy=false; interactionCreateTypes=creates}, Cmd.batch [Cmd.ofMsg GetOpenAIKey; Cmd.ofMsg Ia_LoadChats]

    let createFromSample indexes label backend availableModels sample =
        let chatModel = 
            sample.PreferredModels 
            |> List.tryPick (fun m -> availableModels |> List.tryFind (fun m' -> m' === m)) 
            |> Option.defaultValue availableModels.Head

        let cr,useWeb = 
            match sample.SampleChatType with
            | Simple_Chat useWeb -> CreateChat backend,useWeb
            | QA_Chat _ -> CreateQA backend,false
            | DocQA_Chat _ -> (CreateDocQA (backend,label)),false

        let idxRefs =                        
            match sample.SampleChatType with
            | QA_Chat idx       -> let idxs = idx.Split([|',';' '|],StringSplitOptions.RemoveEmptyEntries) in idxs |> Seq.map(fun idx ->  Azure {Name=idx; Description=""}) |> Seq.toList
            | DocQA_Chat idx    -> let idxs = idx.Split([|',';' '|],StringSplitOptions.RemoveEmptyEntries) in idxs |> Seq.map(fun idx ->  Azure {Name=idx; Description=""}) |> Seq.toList
            | _                 -> []
            
        let idxRefs = 
            idxRefs
            |> List.choose(            
                function Azure x -> 
                            indexes 
                            |> List.tryFind(function Azure  n -> n.Name=x.Name))
        
        let _,ch = Interaction.create cr (Some sample.SampleQuestion) 

        ch 
        |> setUseWeb useWeb
        |> Interaction.setParameters {ch.Parameters with Temperature=sample.Temperature; ChatModel=chatModel}                        
        |> updateBag (ch |> Interaction.qaBag |> Option.map(fun b -> {b with MaxDocs=sample.MaxDocs}))
        |> updateIndx idxRefs
        |> Interaction.setSystemMessage sample.SystemMessage


    let addSamples label (samples:SamplePrompt list) model =
        try
            let backend = 
                model.serviceParameters 
                |> Option.map(fun p -> if not(p.AZURE_OPENAI_ENDPOINTS.IsEmpty) then AzureOpenAI else OpenAI) 
                |> Option.defaultValue OpenAI

            let searchConfigured =
                model.serviceParameters
                |> Option.map(fun x -> not(x.AZURE_SEARCH_ENDPOINTS.IsEmpty))
                |> Option.defaultValue false

            let availableModels =
                match backend with 
                | AzureOpenAI -> 
                    model.serviceParameters 
                    |> Option.bind (fun s -> s.AZURE_OPENAI_MODELS |> Option.map (fun m -> m.CHAT))
                    |> Option.defaultValue []
                | OpenAI ->
                    model.serviceParameters
                    |> Option.bind (fun s -> s.OPENAI_MODELS |> Option.map(fun m->m.CHAT))
                    |> Option.defaultValue []            

            if availableModels.IsEmpty then failwith "No chat models configured"

            let chats = samples |> List.map (createFromSample model.indexRefs label backend availableModels)
            
            let chats = 
                chats 
                |> List.choose (fun c -> 
                    match c.InteractionType with  
                    | Chat _ -> Some c 
                    | _ when searchConfigured -> Some c
                    | _ -> None)
                |> List.choose (isAllowedSample model.appConfig)
            
            let m = {model with interactions = model.interactions @ chats}           
            m,Cmd.none
        with ex ->
            model,Cmd.ofMsg(ShowError ex.Message)

    let badgeColorChat c =
        match c.InteractionType with 
        | QA _   -> Color.Info
        | Chat _ -> Color.Warning
        | DocQA _ -> Color.Tertiary

    let badgeColorCreate createType = 
        match createType with
        | CreateQA _ -> Color.Info
        | CreateChat _ -> Color.Warning
        | CreateDocQA _ -> Color.Tertiary
            
    let createMenuGroup group dispatch =
        concat {
            for (icon,name,createType) in group do 
                comp<MudMenuItem> {
                    "Icon" => icon
                    on.click(fun _ -> dispatch (Ia_Add createType))
                    attr.callback "OnTouch" (fun (e:TouchEventArgs) -> dispatch (Ia_Add createType)) //touch handlers needed for mobile
                    comp<MudPaper> {
                        "Class" => "d-flex align-center"
                        "Elevation" => 0
                        comp<MudBadge> {
                            "Class" => "d-flex flex-none mr-2"
                            "Color" => badgeColorCreate createType
                            "Dot" => true
                            "Left" => true
                        }
                        text name
                    }
                }
        }   

    let createMenu model dispatch =
        let createTypes = model.interactionCreateTypes |> List.choose (isAllowedCreate model.appConfig)
        let groups = 
            createTypes
            |> List.groupBy backend
            |> List.map snd      
        if groups.Length = 1 then 
            createMenuGroup groups.[0] dispatch
        else
            concat {
                comp<MudNavMenu> {
                    for g in groups do
                        yield
                            comp<MudNavGroup> {
                                "Title" => $"Backend: {backend g.[0]}"
                                createMenuGroup g dispatch                          
                            } 
                }
            }
