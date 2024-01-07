namespace FsOpenAI.Client
open System
open System.Net.Http
open System.Threading.Tasks
open Elmish
open FSharp.Control
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Blazored.LocalStorage
open MudBlazor
open FsOpenAI.Client.Interactions
open System.Collections.Generic

type UpdateParms = 
    {
        localStore          : ILocalStorageService
        snkbar              : ISnackbar
        navMgr              : NavigationManager
        httpFac             : IHttpClientFactory
        serverDispatch      : ClientInitiatedMessages -> unit
        serverCall          : ClientInitiatedMessages -> Task            
    }

module Init =
    open MudBlazor
    open Bolero.Html

    let private (===) (a:string) (b:string) = a.Equals(b,StringComparison.InvariantCultureIgnoreCase)
    let private updateBag bag ch = match bag with Some b -> Interaction.setQABag b ch | None -> ch
    let private updateIndx idxs ch = (ch,idxs) ||> List.fold (fun ch i -> Interaction.addIndex i ch) 
    let private setUseWeb useWeb ch = match ch.InteractionType with Chat cbag -> Interaction.setUseWeb useWeb ch | _ -> ch

    let defaultBackend model = model.appConfig.EnabledBackends |> List.tryHead |> Option.defaultValue OpenAI

    let newInteractionTypes templates = 
        let createsBase =
            [
                Icons.Material.Outlined.Chat, "New Chat", CreateChat 
                Icons.Material.Outlined.QuestionAnswer, "New Index Q&A", CreateQA 
            ]
        let createsTemplates = 
            templates
            |> List.map(fun t -> Icons.Material.Outlined.Book, $"New '{t.Label}' Document Query", CreateDocQA t.Label)
        createsBase @ createsTemplates

    let pingServer serverDispatch =
        async{
            do! Async.Sleep 100
            printfn "sending connect to server"
            serverDispatch (ClientInitiatedMessages.Clnt_Connected "me")
        }
        |> Async.StartImmediate

    let isAllowedSample appConfig ch =
        appConfig.EnabledBackends 
        |> List.tryFind (fun b -> b = ch.Parameters.Backend)
        |> Option.bind(fun _ -> 
            match ch.InteractionType with
            | Chat _  when not appConfig.EnableVanillaChat -> None
            | DocQA _ when not appConfig.EnableDocQuery    -> None
            | _                                            -> Some ch)

    let isAllowedCreate appConfig create =
        match create with
        | (_,_,CreateChat) when not appConfig.EnableVanillaChat   -> None
        | (_,_,CreateDocQA _) when not appConfig.EnableDocQuery   -> None
        | _                                                       -> Some create

    let postServerInit model =
        {model with busy=false}, Cmd.batch [Cmd.ofMsg GetOpenAIKey; Cmd.ofMsg Ia_LoadChats]

    let candidateIndexes (idxs:string) =
       let idxs = idxs.Split([|',';' '|],StringSplitOptions.RemoveEmptyEntries) 
       idxs |> Seq.collect(fun n ->  [Azure n; Virtual n]) |> Seq.toList 

    let createFromSample searchAvailable backend indexes label sample =

        let cr,useWeb = 
            match sample.SampleChatType with
            | Simple_Chat useWeb -> CreateChat,useWeb
            | QA_Chat _ -> CreateQA,false
            | DocQA_Chat _ -> (CreateDocQA label),false

        let useWeb = searchAvailable && useWeb

        let idxRefs =                        
            match sample.SampleChatType with
            | QA_Chat idxs     -> candidateIndexes idxs 
            | DocQA_Chat idxs  -> candidateIndexes idxs
            | _                -> []
            
        let idxRefs = 
            idxRefs
            |> List.choose(fun idx -> indexes |> List.tryFind (fun t -> t = idx))
        
        let _,ch = Interaction.create cr backend None
        let ch = Interaction.setQuestion sample.SampleQuestion ch

        ch 
        |> setUseWeb useWeb
        |> Interaction.setParameters {ch.Parameters with Mode=sample.SampleMode}                        
        |> updateBag (ch |> Interaction.qaBag |> Option.map(fun b -> {b with MaxDocs=sample.MaxDocs}))
        |> updateIndx idxRefs
        |> Interaction.setSystemMessage sample.SampleSysMsg

    let flatten (trees:IndexTree list)  =
        let rec loop acc = function
            | [] -> acc
            | x::xs -> 
                let acc = x::acc
                let acc = loop acc x.Children
                loop acc xs
        loop [] trees

    let addSamples label (samples:SamplePrompt list) model =
        try
            let backend = defaultBackend model

            let searchAvailable = 
                model.serviceParameters 
                |> Option.bind(fun x->x.BING_ENDPOINT) 
                |> Option.map (fun x  -> Utils.isEmpty x.API_KEY |> not) 
                |> Option.defaultValue false

            let searchConfigured =
                model.serviceParameters
                |> Option.map(fun x -> not(x.AZURE_SEARCH_ENDPOINTS.IsEmpty))
                |> Option.defaultValue false

            let chatModels =
                model.appConfig.ModelsConfig.ShortChatModels
                @ model.appConfig.ModelsConfig.LongChatModels

            let availableModels =
                match backend with 
                | AzureOpenAI ->  chatModels |> List.filter (fun m -> m.Backend = AzureOpenAI)                   
                | OpenAI -> chatModels |> List.filter (fun m -> m.Backend = OpenAI)
                |> List.map(fun x->x.Model)

            if availableModels.IsEmpty then failwith "No chat models configured"

            let chats = 
                let indexRefs = model.indexTrees |> flatten |> List.map(fun x -> x.Idx) |> List.distinct
                samples |> List.map (createFromSample searchAvailable backend indexRefs label)
            
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
        | CreateQA -> Color.Info
        | CreateChat -> Color.Warning
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
        let createTypes = 
            newInteractionTypes model.templates 
            |> List.choose (isAllowedCreate model.appConfig)
        createMenuGroup createTypes dispatch

    let flashBanner (uparms:UpdateParms) model msg =        
        let txClr = Colors.Pink.Lighten3
        let msgClr = Colors.Grey.Lighten3
        let n = 
            div {
                comp<MudPaper> {
                    "Class" => "d-flex align-center flex-row"
                    "Style" => "background:transparent; box-shadow:none;"
                    comp<MudPaper> {
                        "Class" => "d-flex align-center flex-column"
                        "Style" => "background:transparent; box-shadow:none;"
                        comp<MudImage> {
                            "Style" => "height: 5rem; width: 5rem; object-fit: contain; border-radius:25px"
                            "Elevation" => 5
                            "Src" => "app/imgs/persona.png"
                        }
                        comp<MudText> {
                            "Class" => "ml-2"
                            "Style" => $"color:{txClr}"
                            "Typo" => Typo.subtitle1
                            text (model.appConfig.PersonaText |> Option.defaultValue "Welcome!")
                        }
                    }
                    comp<MudText> {
                        "Class" => "ml-2"
                        "Style" => $"color:{msgClr}; max-width:10rem"                       
                        text msg
                    }
                }
            }
        let rf = RenderFragment(fun (t) -> n.Invoke(null,t,0) |> ignore)
        uparms.snkbar.Add(rf)
        |> ignore
        