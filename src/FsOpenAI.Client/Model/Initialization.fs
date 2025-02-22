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
open Radzen.Blazor

type UpdateParms =
    {
        localStore           : ILocalStorageService
        notificationService  : NotificationService
        dialogService        : DialogService
        navMgr               : NavigationManager
        httpFac              : IHttpClientFactory
        serverConnect        : unit -> Task<unit>
        serverDispatch       : ClientInitiatedMessages -> unit
        serverCall           : ClientInitiatedMessages -> Task
        getInitConfig        : unit -> Task<LoadConfig>
    }

module Init =
    open Bolero.Html
    open Bolero
    open Radzen.Blazor
    open System.Collections.Generic

    let private (===) (a:string) (b:string) = a.Equals(b,StringComparison.InvariantCultureIgnoreCase)

    let defaultBackend model = model.appConfig.EnabledBackends |> List.tryHead |> Option.defaultValue OpenAI

    let pingServer (serverConnect,serverDispatch) =
        task{
            do! Async.Sleep 100
            printfn "sending connect to server"
            do! serverConnect() 
            serverDispatch (ClientInitiatedMessages.Clnt_Connected "me")
        }

    let inline delay (time:int,ret) = 
        async{
            do! Async.Sleep time
            return ret
        }

    ///Invoked after all init. data has been sent by server to a newly connected client.
    ///Includes: service parameters, app configuration, and samples.
    ///Begin rest of the client initialization process
    let postServerInit model =
        let model = {model with busy=false}
        let persistingChats = Model.isChatPeristenceConfigured model
        let postInitMsgs =
            [
                Cmd.ofMsg GetOpenAIKey
                match persistingChats with
                | true  -> Cmd.ofMsg Ia_Session_Load
                | false -> Cmd.ofMsg Ia_Local_Load  
                Cmd.OfAsync.perform delay (1000,()) CloseBanner                
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
            | SM_CodeEval        -> M_CodeEval,false

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
        | SM_CodeEval    -> Interaction.setMode M_CodeEval ch

    let flatten (trees:IndexTree list)  =
        let rec loop acc = function
            | [] -> acc
            | x::xs ->
                let acc = x::acc
                let acc = loop acc x.Children
                loop acc xs
        loop [] trees
    
    //can create sample if corresponding mode is enabled
    let canCreateSample model = function
        | SM_Plain _     -> Model.isEnabled M_Plain model
        | SM_IndexQnA _  -> Model.isEnabled M_Index model || Model.isEnabled M_Doc_Index model
        | SM_QnADoc      -> Model.isEnabled M_Doc model
        | SM_CodeEval    -> Model.isEnabled M_CodeEval model

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
            model.appConfig.ModelsConfig.ChatModels

        let availableModels =
            match backend with
            | AzureOpenAI ->  chatModels |> List.filter (fun m -> m.Backend = AzureOpenAI)
            | OpenAI -> chatModels |> List.filter (fun m -> m.Backend = OpenAI)            
            |> List.map(fun x->x.Model)

        if availableModels.IsEmpty then failwith "No chat models configured"

        let indexRefs = model.indexTrees |> flatten |> List.map(fun x -> x.Idx) |> List.distinct
        samples 
        |> List.filter (fun s -> canCreateSample model s.SampleChatType )
        |> List.map (createFromSample model webSearchConfigured backend indexRefs label)

    let addSamples label (samples:SamplePrompt list) model =
        try
            let chats = createFromSamples (label,samples) model
            let m = {model with interactions = model.interactions @ chats}
            m,Cmd.none
        with ex ->
            model,Cmd.ofMsg(ShowError ex.Message)

    type FlashBanner() =
        inherit ElmishComponent<Model,Message>()

        override this.View model dispatch =
            comp<RadzenCard> {
                comp<RadzenStack> {
                    "Orientation" => Orientation.Vertical
                    "AlignItems" => AlignItems.Center
                    comp<RadzenImage> {
                        "Style" => "height: 10rem; width: 10rem; object-fit: contain; border-radius:25px;"
                        "Src" => "app/imgs/persona.png"
                    }
                    comp<RadzenText> {
                        "Text" => (model.appConfig.PersonaText |> Option.defaultValue "Welcome!")
                    }
                    match model.appConfig.PersonaSubText with
                    | Some t -> 
                        comp<RadzenText> { 
                            "TextStyle" => TextStyle.Caption
                            "Text" => t 
                        }
                    | None -> ()
                }
            }

    let flashBanner (uparms:UpdateParms) model =
        let opts = DialogOptions( ShowTitle = false, Style = "min-height:auto;min-width:auto;width:auto;background:transparent;top:7rem", CloseDialogOnEsc = true)
        let dummyDispatch (m:Message) = ()
        let parms = ["Model", model :> obj; "Dispatch", dummyDispatch] |> dict |> Dictionary
        uparms.dialogService.OpenAsync<FlashBanner>("", parameters=parms, options=opts) |> ignore

    let checkStartBanner (uparms:UpdateParms) model =
       if model.flashBanner && model.appConfig.PersonaText.IsSome then flashBanner uparms model
       let model = {model with flashBanner=false}
       model,Cmd.none

