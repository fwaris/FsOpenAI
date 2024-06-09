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
                                        comp<MudPaper> {                                           
                                            comp<MudText> {
                                                text "Tab header content"
                                            }
                                            comp<MudPaper> {
                                                "Class" => "overflow-auto"
                                                "Style" => "height: 100vh; padding-bottom: 20rem"
                                                comp<MudList> {
                                                    "Class" => "d-flex flex-column"
                                                    "Dense" => true
                                                    concat {
                                                        for i in 1 .. 3 do 
                                                            comp<MudListItem> {
                                                                comp<MudPaper> {
                                                                    "Class" => "d-flex flex-row justify-content-between"
                                                                    comp<MudText> {text $"Item {i}"}
                                                                    comp<MudIconButton> {
                                                                        "Icon" => Icons.Material.Filled.Delete
                                                                        "Color" => Color.Primary                                                            
                                                                    }
                                                                }
                                                            }

                                                    }
                                                }
                                            }
                                        }
                                    }
                            }                            
                        }
                    }
                }
            }         
            comp<MudPopover> {
                "Style" => "height:11rem; width:100%; max-width:60rem;"
                "Open" => true
                "Fixed" => false
                "AnchorOrigin" => Origin.BottomCenter
                "TransformOrigin" => Origin.BottomCenter
                comp<MudPaper> {
                    comp<MudText> {text "Question goes here ..."}
                }
            }
        }
