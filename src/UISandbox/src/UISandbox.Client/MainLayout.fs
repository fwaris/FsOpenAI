namespace UISandbox.Client
open System
open Bolero.Html
open MudBlazor
open Bolero
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web

type Model =
    {
        x: string
        selectedChatId: string option
    }

type Message =
    | Ping

module Test = 
    let testView() = 
        comp<MudPaper> {
            "Style" => "width:80%;max-height:500px;max-width:500px;"
            "Class" => "pa-2 d-flex flex-row"
            "AnchorOrigin" => Origin.BottomCenter
            "TransformOrigin" => Origin.BottomCenter
            "Elevation" => 6
            "Paper" => true
            let colorUp = Color.Success //if fb.ThumbsUpDn > 0 then Color.Success else Color.Default
            let colorDn = Color.Error //if fb.ThumbsUpDn < 0 then Color.Error else Color.Default
            comp<MudTextField<string>> {
                "Class" => "d-flex flex-grow-1 pa-2"
                "Placeholder" => "Comment (optional)"
                "Label" => "Feedback"
                "MaxLines" => 3
                "Value" => ""
                "Variant" => Variant.Filled
            }
            comp<MudPaper> {
                "Class" => "pa-2"
                "Style" => "width: 7rem; height: 7rem;"
                comp<MudGrid> {
                    "Dense" => true
                    comp<MudItem> {
                        "xs" => 6
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Outlined.ThumbUp
                            "Color" => colorUp
                            // on.click (fun _ -> dispatch (Ia_Feedback_Set(chat.Id, {fb with ThumbsUpDn = if fb.ThumbsUpDn > 0 then 0 else 1})))
                        }                    
                    }
                    comp<MudItem> {
                        "xs" => 6
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Outlined.ThumbDown
                            "Color" => colorDn
                            // on.click (fun _ -> dispatch (Ia_Feedback_Set(chat.Id, {fb with ThumbsUpDn = if fb.ThumbsUpDn < 0 then 0 else -1})))
                        }  
                    }
                    comp<MudItem> {
                        "xs" => 6
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Outlined.Done
                            "Title" => "Submit"
                            // on.click (fun _ -> 
                            //     dispatch (Ia_ToggleFeedback(chat.Id))
                            //     dispatch (Ia_Feedback_Submit(chat.Id)))
                        }
                    }         
                    comp<MudItem> {
                        "xs" => 6 
                        comp<MudIconButton> {
                            "Icon" => Icons.Material.Outlined.Cancel
                            "Title" => "Close"
                        //     on.click (fun _ -> 
                        //         dispatch (Ia_ToggleFeedback(chat.Id))
                        //         dispatch (Ia_Feedback_Set(chat.Id,fb)))
                        }
                    }  
                }
            }
        }   


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
        this.Selected <- model.selectedChatId
        concat {
            comp<PageTitle> { text "Sandbox" }
            comp<MudThemeProvider> { "isDarkMode" => false}
            comp<MudScrollToTop> {comp<MudFab> { "Icon" => Icons.Material.Filled.ArrowUpward; "Color" => Color.Primary; "Size" => Size.Small }}
            comp<MudDialogProvider> {attr.empty()}
            comp<MudSnackbarProvider> {attr.empty()}                
            comp<MudLayout> {
                "Style" => "height:100vh; overflow: hidden;"
                AppBar.appBar model dispatch
                comp<MudMainContent> {
                    //"Style" => "height:100%; overflow: hidden; margin-bottom: 11rem;"
                    concat {
                        comp<MudDynamicTabs> {                                                       
                            "AddTabIcon" => ""
                            "Outlined" => true
                            tabs
                            concat {
                                for c in 0..1 do
                                    comp<MudTabPanel> {
                                        "Id" => string c
                                        "Text" => $"Tab {c}"
                                        "tag" => c
                                        "BadgeDot" => true
                                        "ShowCloseIcon" => true
                                        Test.testView()
                                    }
                            }                            
                        }
                    }
                }
            }         
        }
