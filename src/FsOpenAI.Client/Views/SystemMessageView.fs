namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared

type SystemMessageView() =
    inherit ElmishComponent<Interaction,Message>()
    
    override this.View chat dispatch =
        let msg = chat.SystemMessage
        comp<MudExpansionPanels> {
            "Class" => "d-flex flex-1"
            comp<MudExpansionPanel> {                
                "Text" => "System Message"
                "Icon" => Icons.Material.Filled.Try
                //"DisableGutters" => true
                "Dense" => true
                comp<MudTextField<string>> {              
                    "Class" => "ma-2"
                    attr.callback "ValueChanged" (fun e -> dispatch (Ia_SystemMessage (chat.Id,e)))
                    "Variant" => Variant.Outlined
//                    "Label" => "System Prompt"
                    "Lines" => 10
                    "Placeholder" => "Set the 'tone' of the model"
                    "Text" => msg
                }                 
            }            
        }

type SystemMessageShortView() =
    inherit ElmishComponent<Interaction*Model,Message>()
    
    override this.View m dispatch =
        let chat,model = m
        let msg = chat.SystemMessage        
        let isOpen = TmpState.isSysMsgOpen chat.Id model
        concat {
            comp<MudIconButton> { 
                "Class" => "d-flex flex-none align-self-center ma-2"
                "Icon" => Icons.Material.Outlined.Try
                "Title" => "System message"
                on.click(fun e -> dispatch (Ia_ToggleSysMsg chat.Id))
            }
            comp<MudPopover> {
                    "Style" => "max-width:500px; width:80%"
                    "AnchorOrigin" => Origin.TopLeft
                    "TransformOrigin" => Origin.TopLeft
                    "Open" => isOpen
                    comp<MudPaper> {
                        "Outlined" => true
                        "Class" => "py-4"
                        comp<MudStack> {
                            comp<MudStack> {
                                "Row" => true
                                comp<MudText> {
                                    "Class" => "px-4"
                                    "Typo" => Typo.h6
                                    "System Message"
                                }
                                comp<MudSpacer>{
                                    attr.empty()
                                }
                                comp<MudIconButton> {
                                    "Class" => "align-self-end"
                                    "Icon" => Icons.Material.Filled.Close
                                    on.click (fun e -> dispatch (Ia_ToggleSysMsg chat.Id))
                                }
                            }
                            comp<MudTextField<string>> {        
                                "Class" => "ma-2"
                                attr.callback "ValueChanged" (fun e -> dispatch (Ia_SystemMessage (chat.Id,e)))
                                "Variant" => Variant.Outlined
                                "Lines" => 10
                                "Placeholder" => "Set the 'tone' of the model"
                                "Text" => msg
                            }
                    }
                }
            }
        }
