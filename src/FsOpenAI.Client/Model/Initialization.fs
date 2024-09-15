namespace FsOpenAI.Client
open System
open System.Net.Http
open System.Threading.Tasks
open Elmish
open FSharp.Control
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Blazored.LocalStorage
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open Radzen

type UpdateParms =
    {
        localStore           : ILocalStorageService
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

    let defaultBackend model = model.appConfig.EnabledBackends |> List.tryHead |> Option.defaultValue OpenAI

    let pingServer serverDispatch =
        async{
            do! Async.Sleep 100
            printfn "sending connect to server"
            serverDispatch (ClientInitiatedMessages.Clnt_Connected "me")
        }
        |> Async.StartImmediate

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

    let createFromSample model searchAvailable backend indexes label sample =

        let mode,useWeb =
            match sample.SampleChatType with
            | SM_Plain useWeb    -> M_Plain,useWeb
            | SM_QnADoc          -> M_Doc,false
            | SM_IndexQnA _      -> M_Index,false

        let useWeb = searchAvailable && useWeb

        let idxRefs =
            match sample.SampleChatType with
            | SM_IndexQnA idxs         -> candidateIndexes idxs
            | _                        -> []

        let idxRefs =
            idxRefs
            |> List.choose(fun idx -> indexes |> List.tryFind (fun t -> t = idx))

        let _,ch = Interaction.create mode backend None
        let ch = 
            ch
            |> Interaction.setQuestion sample.SampleQuestion
            |> Interaction.setSystemMessage sample.SampleSysMsg
            |> Interaction.setParameters {ch.Parameters with Mode=sample.SampleMode}

        match sample.SampleChatType with
        | SM_Plain _     -> Interaction.setUseWeb useWeb ch 
        | SM_IndexQnA _  -> Interaction.setQABag {QABag.Default with Indexes = idxRefs; MaxDocs=model.appConfig.DefaultMaxDocs} ch
        | SM_QnADoc      -> ch

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

        let webSearchConfigured =
            model.serviceParameters
            |> Option.bind(fun x->x.BING_ENDPOINT)
            |> Option.map (fun x  -> Utils.isEmpty x.API_KEY |> not)
            |> Option.defaultValue false

        let aiSearchConfigured =
            model.serviceParameters
            |> Option.map(fun x -> not(x.AZURE_SEARCH_ENDPOINTS.IsEmpty))
            |> Option.defaultValue false

        if Model.isEnabled M_Index model && not aiSearchConfigured then failwith "Index endpoints not configured"

        let chatModels =
            model.appConfig.ModelsConfig.ShortChatModels
            @ model.appConfig.ModelsConfig.LongChatModels

        let availableModels =
            match backend with
            | AzureOpenAI ->  chatModels |> List.filter (fun m -> m.Backend = AzureOpenAI)
            | OpenAI -> chatModels |> List.filter (fun m -> m.Backend = OpenAI)            
            |> List.map(fun x->x.Model)

        if availableModels.IsEmpty then failwith "No chat models configured"

        let indexRefs = model.indexTrees |> flatten |> List.map(fun x -> x.Idx) |> List.distinct
        samples |> List.map (createFromSample model webSearchConfigured backend indexRefs label)

    let addSamples label (samples:SamplePrompt list) model =
        try
            let chats = createFromSamples (label,samples) model
            let m = {model with interactions = model.interactions @ chats}
            m,Cmd.none
        with ex ->
            model,Cmd.ofMsg(ShowError ex.Message)

    let flashBanner (uparms:UpdateParms) model msg =
        div{
            text "work in progress ..."
        }
    (*
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
    *)


