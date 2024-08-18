namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open MudBlazor
open FsOpenAI.Client
open Microsoft.AspNetCore.Components
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions

module attr' =
    let inline callback (name: string) ([<InlineIfLambda>] value: unit -> unit) =
        Attr(fun receiver builder sequence ->
            builder.AddAttribute(sequence, name, EventCallback.Factory.Create(receiver, Action(value)))
            sequence + 1)

type MainContent() =
    inherit ElmishComponent<Model,Message>()

    let tabs = Ref<MudDynamicTabs>()
    member val Selected : string option = None with get, set

    override this.OnAfterRender(d) =
        match this.Selected with
        | Some id ->
            tabs.Value
            |> Option.bind (fun t -> t.Panels |> Seq.tryFind (fun p -> p.ID = id))
            |> Option.iter(fun p ->
                let t = tabs.Value.Value
                if t.ActivePanel.ID <> p.ID then
                    t.ActivatePanel(p))
        | None -> ()

    override this.View model dispatch =
        this.Selected <- model.selectedChatId
        let flexDir = if model.tabsUp then "flex-column" else "flex-row"
        concat {
            comp<MudPaper> {
                "Style" => "position: fixed; width:100%;"
                comp<MudPaper> {
                    "Class" => $"d-flex {flexDir}"
                    comp<MudDynamicTabs> {
                        "AddTabIcon" => if model.tabsUp then Icons.Material.Outlined.KeyboardDoubleArrowLeft else Icons.Material.Outlined.KeyboardDoubleArrowUp
                        "Outlined" => true
                        "Position" => if model.tabsUp then Position.Top else Position.Left
                        attr.callback "CloseTab" (fun (t:MudTabPanel) -> let c = t.Tag :?> Interaction in dispatch (Ia_Remove c.Id))
                        attr'.callback "AddTab" (fun _ -> dispatch ToggleTabs)
                        attr.callback "ActivePanelIndexChanged" (fun (i:int) ->
                            let id = tabs.Value.Value.ActivePanel.ID :?> string
                            dispatch (Ia_Selected id))
                        tabs
                        concat {
                            for c in model.interactions do
                                comp<MudTabPanel> {
                                    "Id" => c.Id
                                    "Text" => Interaction.label c
                                    "tag" => c
                                    "BadgeColor" => Init.badgeColorChat c
                                    "BadgeDot" => true
                                    "ShowCloseIcon" => true
                                }
                            }
                    }
                    comp<MudPaper> {
                        "Class" => "d-flex flex-column flex-grow-1 align-content-center"
                        match model.selectedChatId with
                        | None  -> div {text "..."}
                        | Some id ->
                            let c = model.interactions |> Seq.tryFind (fun c -> c.Id = id)
                            match c with
                            | Some c ->
                                let t = c.InteractionType
                                let cfg = model.appConfig
                                match t with
                                | Plain _ when Init.isAllowedChat cfg t           -> ecomp<PlainView,_,_> (c,model) dispatch {attr.empty()}
                                | IndexQnA bag when Init.isAllowedChat cfg t      -> ecomp<IndexQnAView,_,_> (bag,c,model) dispatch {attr.empty()}
                                | IndexQnADoc dbag when Init.isAllowedChat cfg t  -> ecomp<IndexQnADocView,_,_> (dbag,c,model) dispatch {attr.empty()}
                                | QnADoc dc when Init.isAllowedChat cfg t         -> ecomp<QnADocView,_,_> (dc,c,model) dispatch {attr.empty()}
                                | CodeEval wb when Init.isAllowedChat cfg t       -> ecomp<CodeEvalView,_,_> (wb,c,model) dispatch {attr.empty()}
                                | _                                               -> ()
                            | None -> ()
                        ecomp<ChatHistoryView,_,_> model dispatch { attr.empty() }
                    }
                }
            }
        }
