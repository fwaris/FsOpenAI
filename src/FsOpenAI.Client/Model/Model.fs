﻿namespace FsOpenAI.Client
open System
open Bolero
open FsOpenAI.Shared
open System.Security.Claims

type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/authentication/{action}">] Authentication of action:string //need a separate route for authentication

type SourceType = ST_AIModel | ST_WebSearch

[<CLIMutable>]
type SourceTree = {
    NodeType: SourceType
    Description: string
    Children: SourceTree list
}
with 
    static member Flattended (xs:SourceTree list) = 
        let rec loop acc xs = 
            match xs with 
            | [] -> acc
            | x::rest -> Set.union (loop (Set.add x acc) x.Children) (loop acc rest)
        loop Set.empty xs

type TempChatState =
    {
        SettingsOpen: bool
        DocsOpen:string option
        DocDetailsOpen:bool
        PromptsOpen:bool
        IndexOpen:bool
        SysMsgOpen:bool
        FeedbackOpen:bool
    }
    with
        static member Default =
            {
                SettingsOpen=false
                DocsOpen=None
                DocDetailsOpen=false
                PromptsOpen=false
                IndexOpen=false
                SysMsgOpen=false
                FeedbackOpen=false
            }

type Model =
    {
        flashBanner             : bool
        page                    : Page
        selectedChatId          : string option
        interactions            : Interaction list
        appConfig               : AppConfig
        loadConfig              : LoadConfig
        samples                 : (string*SamplePrompt list) list
        templates               : LabeledTemplates list
        indexTrees              : IndexTree list
        error                   : string option
        busy                    : bool
        tempChatSettings        : Map<string,TempChatState>
        settingsOpen            : Map<string,bool>
        serviceParameters       : ServiceSettings option
        darkTheme               : bool
        tabsUp                  : bool
        user                    : UserState
        photo                   : string option
    }

type Message =
    | StartInit
    | Ia_SystemMessage of string * string
    | Ia_ApplyTemplate of string*TemplateType*Template
    | Ia_SetPrompt of string*TemplateType*string
    | Ia_Save of string
    | Ia_Session_Save of string
    | Ia_Session_Delete of string
    | Ia_Session_ClearAll
    | Ia_Session_Load
    | Ia_Local_Save
    | Ia_Local_Load
    | Ia_Local_Loaded of Interaction list
    | Ia_Local_ClearAll
    | Ia_ClearChats
    | Ia_ResetChat of string * string
    | Ia_AddMsg of string * InteractionMessage
    | Ia_SetQuestion of string * string
    | Ia_AddDelta of string * string
    | Ia_Completed of string * string option //id and optional error
    | Ia_Restart of string * InteractionMessage
    | Ia_UpdateName of string * string
    | Ia_UpdateParms of string * InteractionParameters
    | Ia_Add of InteractionMode
    | Ia_Remove of string
    | Ia_Selected of string
    | Ia_UpdateQaBag of string * QABag
    | Ia_UpdateCodeEvalParms of string * CodeEvalParms
    | Ia_Notification of string * string
    | Ia_File_BeingLoad2 of string * DocumentContent
    | Ia_File_Load of string
    | Ia_File_Loaded of string*string
    | Ia_File_SetContents of string*string*bool
    | Ia_SetSearch of string*string
    | Ia_UseWeb of string*bool
    | Ia_Mode_Document of string
    | Ia_Mode_CodeEval of string
    | Ia_Remove_Document of string
    | Ia_Mode_Doc_Index of string*bool
    | Ia_Submit of string*string
    | Ia_SubmitOnKey of string*bool
    | Ia_ToggleSettings of string
    | Ia_ToggleSysMsg of string
    | Ia_ToggleDocs of string*string option
    | Ia_ToggleDocDetails of string
    | Ia_TogglePrompts of string
    | Ia_ToggleFeedback of string
    | Ia_OpenIndex of string
    | Ia_SetIndex of string*IndexRef list
    | Ia_Feedback_Set of string*Feedback
    | Ia_Feedback_Submit of string
    //common
    | SidebarExpanded of bool
    | ToggleSideBar
    | CloseBanner of unit
    | SaveUIState
    | LoadUIState of unit
    | LoadedUIState of bool //darkTheme
    | RefreshIndexes of bool
    | Nop of unit
    | Error of exn
    | GetInitConfig
    | GotInitConfig of LoadConfig
    | ShowError of string
    | ShowInfo of string
    | FlashBanner
    | ClearError
    | OpenCloseSettings of string
    | FromServer of ServerInitiatedMessages
    | GetOpenAIKey
    | SetOpenAIKey of string
    | UpdateOpenKey of string
    | PurgeLocalData
    | SaveToLocal of string*obj
    | IgnoreError of exn
    | SetPage of Page
    | SetAuth of ClaimsPrincipal option
    | LoginLogout
    | GetUserDetails
    | GotUserDetails of string option

module Model =
    open Elmish
    open Bolero.Html
    let initModel =
        {
            flashBanner = true
            interactions = []
            templates = []
            appConfig = AppConfig.Default
            loadConfig = LoadConfig.Default
            samples = []
            indexTrees = []
            error = None
            busy = false
            tempChatSettings = Map.empty
            settingsOpen = Map.empty
            serviceParameters = None
            darkTheme = true
            user = Unauthenticated
            page = Home
            photo = None
            selectedChatId = None
            tabsUp = true
        }

    let selectedChat model =
        match model.selectedChatId with
        | Some id -> model.interactions |> List.tryFind (fun c -> c.Id = id)
        | None    -> None

    let checkBusy model apply =
        if model.busy then
            model,Cmd.none
        else
            apply model

    let isChatPeristenceConfigured model = model.appConfig.SessionTableName.IsSome

    let isEnabled mode model = model.appConfig.EnabledChatModes |> List.exists (fun (x,_) -> x = mode)
    let isEnabledAny modes model = modes |> List.exists (fun m -> isEnabled m model)

    type Blk = M of int*int | Topen of int | Tblock of (int*int) | E of int

    let rec blocks (msg:string) acc (startI:int) =
        let blkStart = msg.IndexOf("```",startI)
        if blkStart < 0 then
            (E startI)::acc
        else
            let blkEnd = msg.IndexOf("```", blkStart+3)
            if blkEnd < 0 then
                (Topen (blkStart+3))::M(startI,blkStart)::acc
            else
                let langEnd = msg.IndexOf("\n",blkStart+3)
                let tblk =
                    if langEnd < 0 then
                        Tblock(blkStart+3,blkEnd)
                    else
                        Tblock(langEnd,blkEnd)
                blocks msg (tblk::M(startI,blkStart)::acc) (blkEnd+3)

    let blockQuotes (msg:string) =
        let blks = blocks msg [] 0 |> List.rev
        concat{
            for b in blks do
                match b with
                | M(i1,i2) -> yield text (msg.Substring(i1,i2-i1))
                | Topen i -> yield pre{code{text (msg.Substring(i))}}
                | Tblock(i1,i2) -> yield pre{attr.style "max-width:100rem;"; code{text (msg.Substring(i1,i2-i1))}}
                | E i -> yield text(msg.Substring(i))
        }
