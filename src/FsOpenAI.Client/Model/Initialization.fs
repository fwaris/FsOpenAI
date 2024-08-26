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
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open Radzen

type UpdateParms =
    {
        localStore           : ILocalStorageService
        snkbar               : ISnackbar
        notificationService  : NotificationService
        dialogService        : DialogService
        navMgr               : NavigationManager
        httpFac              : IHttpClientFactory
        serverDispatch       : ClientInitiatedMessages -> unit
        serverDispatchUnAuth : ClientInitiatedMessages -> unit
        serverCall           : ClientInitiatedMessages -> Task
    }

module Init =
    open Bolero.Html

    let private (===) (a:string) (b:string) = a.Equals(b,StringComparison.InvariantCultureIgnoreCase)
    let private updateBag bag ch = match bag with Some b -> Interaction.setQABag b ch | None -> ch
    let private updateIndx idxs ch = (ch,idxs) ||> List.fold (fun ch i -> Interaction.addIndex i ch)
    let private setUseWeb useWeb ch = Interaction.setUseWeb useWeb ch 

    let defaultBackend model = model.appConfig.EnabledBackends |> List.tryHead |> Option.defaultValue OpenAI

    let newInteractionTypes templates =
        let createsBase =
            [
                Icons.Material.Outlined.QuestionAnswer, "New Index Q&A", Crt_IndexQnA
                Icons.Material.Outlined.DocumentScanner, "New Doc. Q&A", Crt_QnADoc
            ]
        createsBase
        @ [Icons.Material.Outlined.Chat, "New Chat with GPT", Crt_Plain ]

    let pingServer serverDispatch =
        async{
            do! Async.Sleep 100
            printfn "sending connect to server"
            serverDispatch (ClientInitiatedMessages.Clnt_Connected "me")
        }
        |> Async.StartImmediate

    let isAllowedChat appConfig (ctype:InteractionType) =
        appConfig.EnabledChatModes
        |> List.exists (fun (m,_) ->
            match m,ctype with
            | CM_Plain, Plain _
            | CM_IndexQnA,IndexQnA _
            | CM_QnADoc, QnADoc _
            | CM_TravelSurvey, CodeEval _ -> true
            | _                           -> false)

    let isAllowedSample appConfig ch =
        appConfig.EnabledBackends
        |> List.tryFind (fun b -> b = ch.Parameters.Backend)
        |> Option.map(fun _ -> ch.Types |> List.exists (fun t -> isAllowedChat appConfig t))
        |> Option.defaultValue false

    let isAllowedCreate appConfig (ctype:InteractionCreateType) =
        appConfig.EnabledChatModes
        |> List.exists (fun (m,_) ->
            match m,ctype with
            | CM_Plain, Crt_Plain
            | CM_QnADoc, Crt_QnADoc
            | CM_IndexQnA,Crt_IndexQnA          -> true
            | _                                 -> false)

    ///Invoked after all init. data has been sent by server to a newly connected client.
    ///Includes: service parameters, app configuration, and samples.
    ///Begin rest of the client initialization process
    let postServerInit model =
        let model = {model with busy=false}
        let persistingChats = Model.isChatPeristenceConfigured model
        let postInitMsgs =
            [
                Cmd.ofMsg GetOpenAIKey
                match persistingChats, model.appConfig.RequireLogin with
                | true, true  -> Cmd.ofMsg (FlashInfo "Please login to continue")
                | true, false -> Cmd.ofMsg Ia_Session_Load
                | false, _    -> Cmd.ofMsg Ia_Local_Load                
            ]
        model,Cmd.batch postInitMsgs

    let candidateIndexes (idxs:string) =
       let idxs = idxs.Split([|',';' '|],StringSplitOptions.RemoveEmptyEntries)
       idxs |> Seq.collect(fun n ->  [Azure n; Virtual n]) |> Seq.toList

    let createFromSample searchAvailable backend indexes label sample =

        let cr,useWeb =
            match sample.SampleChatType with
            | SM_Plain useWeb    -> Crt_Plain,useWeb
            | SM_QnADoc          -> Crt_QnADoc,false
            | SM_IndexQnA _      -> Crt_IndexQnA,false

        let useWeb = searchAvailable && useWeb

        let idxRefs =
            match sample.SampleChatType with
            | SM_IndexQnA idxs         -> candidateIndexes idxs
            | _                        -> []

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

    let createFromSamples (label,samples:SamplePrompt list) model =
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
            |> List.choose(fun c -> 
                if searchConfigured then Some c 
                elif c.Types |> List.exists (function Plain _ -> true | _ -> false) then Some c
                else None)
            |> List.filter (isAllowedSample model.appConfig)
        chats

    let addSamples label (samples:SamplePrompt list) model =
        try
            let chats = createFromSamples (label,samples) model
            let m = {model with interactions = model.interactions @ chats}
            m,Cmd.none
        with ex ->
            model,Cmd.ofMsg(ShowError ex.Message)


    let createMenuGroup dispatch group  =
        concat {
            for (icon,name,createType) in group do
                comp<MudMenuItem> {
                    "Icon" => icon
                    on.click(fun _ -> dispatch (Ia_Add createType))                    
                    comp<MudPaper> {
                        "Class" => "d-flex align-center"
                        "Elevation" => 0
                        comp<MudBadge> {
                            "Class" => "d-flex flex-none mr-2"
                            "Dot" => true
                        }
                        text name
                    }
                }
        }

    let createMenu model dispatch =
        newInteractionTypes model.templates
        |> List.filter (fun (_,_,ctype) -> isAllowedCreate model.appConfig ctype)
        |> createMenuGroup dispatch

    let flashBanner (uparms:UpdateParms) model msg =
        let txClr = Colors.Pink.Lighten3
        let msgClr = Colors.Gray.Lighten3
        let n =
            div {
                comp<MudPaper> {
                    "Class" => "d-flex align-center flex-row"
                    "Style" => "background:transparent; box-shadow:none;"
                    comp<MudPaper> {
                        "Class" => "d-flex flex-column"
                        "Style" => "background:transparent; box-shadow:none; justify-content: space-around;"
                        comp<MudImage> {
                            "Style" => "height: 5rem; width: 5rem; object-fit: contain; border-radius:25px"
                            "Elevation" => 5
                            "Src" => "app/imgs/persona.png"
                        }
                        comp<MudText> {
                            "Style" => $"color:{txClr}"
                            "Typo" => Typo.subtitle1
                            text (model.appConfig.PersonaText |> Option.defaultValue "Welcome!")
                        }
                        match model.appConfig.PersonaSubText with
                        | Some t ->
                            comp<MudText> {
                                "Style" => $"color:{txClr}"
                                "Typo" => Typo.body2
                                text t
                            }
                        | None -> ()
                    }
                    comp<MudText> {
                        "Style" => $"color:{msgClr}; max-width:10rem"
                        text msg
                    }
                }
            }
        let rf = RenderFragment(fun (t) -> n.Invoke(null,t,0) |> ignore)
        uparms.snkbar.Add(rf)
        |> ignore

    module AppConfig =
        let setColors (ap:AppPalette) (p:Palette) =
            ap.Primary |> Option.iter (fun c -> p.Primary <- Utilities.MudColor(c))
            ap.Secondary |> Option.iter (fun c -> p.Secondary <- Utilities.MudColor(c))
            ap.Tertiary |> Option.iter (fun c -> p.Tertiary <- Utilities.MudColor(c))
            ap.Info|> Option.iter (fun c -> p.Info <- Utilities.MudColor(c))
            ap.Success |> Option.iter (fun c -> p.Success <- Utilities.MudColor(c))
            ap.Warning |> Option.iter (fun c -> p.Warning <- Utilities.MudColor(c))
            ap.Error |> Option.iter (fun c -> p.Error <- Utilities.MudColor(c))

        let toTheme (appConfig:AppConfig) =
            let pLight = new PaletteLight()
            let pDark = new PaletteDark()
            appConfig.PaletteDark |> Option.iter(fun p -> setColors p pDark)
            appConfig.PaletteLight |> Option.iter(fun p -> setColors p pLight)
            let th = MudTheme()
            th.PaletteDark <- pDark
            th.PaletteLight <- pLight
            th

