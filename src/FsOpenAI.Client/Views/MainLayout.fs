namespace FsOpenAI.Client.Views
open System
open Bolero.Html
open MudBlazor
open FsOpenAI.Client    
open FsOpenAI.Client.Interactions
open Bolero
open Microsoft.AspNetCore.Components.WebAssembly.Authentication
open Microsoft.AspNetCore.Components

type MainLayout() =
    inherit ElmishComponent<Model,Message>()    

    let tabs = Ref<MudDynamicTabs>()

    member val Selected : string option = None with get, set

    override this.OnAfterRender(d) =
        match this.Selected with 
        | Some id -> 
            tabs.Value 
            |> Option.bind (fun t -> t.Panels |> Seq.tryFind (fun p -> p.ID = id))
            |> Option.iter(fun p -> tabs.Value.Value.ActivatePanel(p))
        | None -> ()

    override this.View model dispatch =        
        this.Selected <- model.selected
        match model.page with 
        | Page.Authentication action -> 
            ecomp<LoginRedirectView,_,_> (action,model) dispatch {attr.empty()}
        | Page.Home -> 
            div {            
                comp<MudThemeProvider> { "isDarkMode" => model.darkTheme; "Theme" => model.theme }
                comp<MudDialogProvider> {attr.empty()}
                comp<MudSnackbarProvider> {attr.empty()}                
                comp<MudLayout> {
                    AppBar.appBar model dispatch
                    comp<MudMainContent> {
                        comp<MudDynamicTabs> {                                 
                            "AddTabIcon" => ""
                            "Outlined" => true
                            attr.callback "CloseTab" (fun (t:MudTabPanel) -> let c = t.Tag :?> Interaction in dispatch (Ia_Remove c.Id))
                            attr.callback "ActivePanelIndexChanged" (fun (i:int) -> 
                                let id = tabs.Value.Value.ActivePanel.ID :?> string 
                                dispatch (Ia_Selected id))
                            tabs
                            concat {
                                for c in model.interactions do
                                    comp<MudTabPanel> {
                                        "Id" => c.Id
                                        "Text" => Interaction.name c
                                        "tag" => c
                                        "BadgeColor" => badgeColorChat c
                                        "BadgeDot" => true
                                        "ShowCloseIcon" => true
                                        match c.InteractionType with 
                                        | Chat _ when model.appConfig.EnableVanillaChat  -> ecomp<ChatView,_,_> (c,model) dispatch {attr.empty()}
                                        | QA bag                                         -> ecomp<QAView,_,_> (bag,c,model) dispatch {attr.empty()}
                                        | DocQA dbag when model.appConfig.EnableDocQuery -> ecomp<DocQAView,_,_> (dbag,c,model) dispatch {attr.empty()}
                                        | _                                              -> ()
                                    }
                            }                            
                        }                        
                    }
                }         
            }
