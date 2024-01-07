namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
()
//type PromptView() =
//    inherit ElmishComponent<Chat*ChatMessage,Message>()
    
//    override this.View model dispatch =
//        let chat,message = model
//        concat {  
//            comp<MudGrid> {
//                //"Class" => "d-flex flex-grow-1"
//                "Spacing" => 0
//                comp<MudItem> {
//                    //"Class" => "d-flex flex-grow-1"
//                    "xs" => 12
//                    comp<MudTextField<string>> {
//                        attr.callback "ValueChanged" (fun e -> dispatch (Chat_AddMsg (chat.Id,e)))
//                        "Variant" => Variant.Outlined
//                        "Label" => "Prompt"
//                        "Lines" => 15
//                        "Placeholder" => "Enter prompt or question"
//                        "Text" => message.Message
//                    }
//                }
//                comp<MudItem> {               
//                    //"Class" => "d-flex flex-grow-0"
//                    "xs" => 12
//                    comp<MudStack> {
//                        "Row" => true
//                        comp<MudIconButton> {
//                            "Icon" => Icons.Material.Filled.Settings
//                        }
//                        comp<MudIconButton> {
//                            "Icon" => Icons.Material.Filled.DeleteSweep
//                            "Tooltip" => "Clear data"
//                            on.click(fun ev -> dispatch Reset )
//                        }
//                        comp<MudSpacer> {attr.empty()}
//                        comp<MudIconButton> {
//                            "Icon" => Icons.Material.Filled.Add
//                            on.click(fun ev -> dispatch AddDummyContent)
//                        }
//                        comp<MudSpacer> {attr.empty()}
//                        comp<MudIconButton> {    
//                            "Icon" => Icons.Material.Filled.Send
//                            on.click(fun ev -> dispatch (SubmitChat chat.Id) )
//                        }
//                    }
//                }
//            }
//        }

