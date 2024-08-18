namespace FsOpenAI.Client.Views
open System
open Bolero
open Bolero.Html
open FsOpenAI.Client
open FsOpenAI.Shared
open Radzen
open Radzen.Blazor
open Microsoft.AspNetCore.Components

type HeaderView() =
    inherit ElmishComponent<Model,Message>()

    [<Inject>] member val ThemeService:ThemeService = Unchecked.defaultof<_> with get,set
    
    override this.View model dispatch = 
        let sidebarExpanded = TmpState.isOpen C.SIDE_BAR_EXPANDED model
        comp<RadzenHeader> {
            attr.``class`` "rz-background-color-danger-dark"
            comp<RadzenStack> {
                "Orientation" => Orientation.Horizontal
                "AlignItems" => AlignItems.Center
                comp<RadzenSidebarToggle> {                
                    "Icon" => (if sidebarExpanded then "chevron_left" else  "chevron_right")
                    attr.callback "Click" (fun (e:EventArgs) -> dispatch ToggleSideBar)
                }
                comp<RadzenLabel> {
                    "Text" => "Header"
                }
                comp<RadzenAppearanceToggle> {attr.empty()}
            }
        }
        
