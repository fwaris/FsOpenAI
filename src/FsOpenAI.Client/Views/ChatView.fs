namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client

type ChatView() =
    inherit ElmishComponent<Chat,Message>()

    override this.View chat dispatch =
        concat {
            ecomp<ChatParametersView,_,_> chat dispatch {attr.empty()}
            ecomp<SysPromptView,_,_> chat dispatch {attr.empty()}
            ecomp<ChatHistoryView,_,_> chat dispatch {attr.empty()}
        }


