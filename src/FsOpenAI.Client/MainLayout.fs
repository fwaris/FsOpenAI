module MainLayout
open System
open Elmish
open Bolero.Html
open MudBlazor
open FsOpenAI.Client.Model

let view (model:Model) dispatch =
    div {        
        comp<MudLayout> {
            comp<MudThemeProvider> { "isDarkMode" => true }
            comp<MudDialogProvider> {attr.empty()}
            comp<MudSnackbarProvider> {attr.empty()}
            AppBar.appBar model
            comp<MudMainContent> {
                comp<MudGrid> {
                    "Class" => "py-8 px-4"
                    comp<MudItem> {
                        "xs" => 4
                        comp<MudStack> {
                            "Class" => "fixed"
                            "Style" => "max-width:30%;"
                            ecomp<SysPrompt.SysPromptView,_,_> model dispatch {attr.empty()}                        
                            ecomp<Prompt.PromptView,_,_> model dispatch {attr.empty()}                        
                        }
                    }
                    comp<MudItem> {
                        //"Class" => "d-flex flex-grow-0 mud-width-full py-3"
                        "xs" => 8
                        ecomp<Chat.ChatView,_,_> model dispatch {attr.empty()}
                    }
                }
            }

        }
    }

