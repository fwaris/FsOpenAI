namespace FsOpenAI.Client.Views
open System
open Bolero.Html
open MudBlazor
open FsOpenAI.Client    
open Bolero

type MainLayout() =
    inherit ElmishComponent<Model,Message>()

    let tabs = Ref<MudDynamicTabs>()
    //make any newly created chat the active chat
    let selChat cs =
        let now = DateTime.Now
        cs 
        |> List.indexed 
        |> List.tryFind (fun  (i,c) -> (now - c.Timestamp).TotalSeconds < 1.0)
        |> Option.map fst
        |> Option.defaultWith(fun _ ->             
            match tabs.Value with
            | Some t -> t.ActivePanelIndex
            | None   -> -1)

    override this.View model dispatch =        
        div {            
            comp<MudThemeProvider> { "isDarkMode" => model.darkTheme }
            comp<MudDialogProvider> {attr.empty()}
            comp<MudSnackbarProvider> {attr.empty()}                
            comp<MudLayout> {
                AppBar.appBar model dispatch
                comp<MudMainContent> {
                    comp<MudDynamicTabs> {                                 
                        "ActivePanelIndex" => selChat model.interactions
                        "AddTabIcon" => ""
                        attr.callback "CloseTab" (fun (t:MudTabPanel) -> let c = t.Tag :?> Interaction in dispatch (Ia_Remove c.Id))
                        tabs
                        concat {
                            for c in model.interactions do
                                comp<MudTabPanel> {
                                    "Text" => c.Name
                                    "tag" => c
                                    "ShowCloseIcon" => true
                                    match c.InteractionType with 
                                    | Chat _ -> ecomp<ChatView,_,_> (c,model) dispatch {attr.empty()}
                                    | QA bag -> ecomp<QAView,_,_> (bag,c,model) dispatch {attr.empty()}
                                }
                        }                            
                    }                        
                }
            }         
        }
