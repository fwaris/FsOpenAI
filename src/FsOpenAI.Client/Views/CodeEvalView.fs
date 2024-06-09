namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions.Wholesale

type CodeEvalViewPopup() =
    inherit ElmishComponent<CodeEvalBag*Interaction*Model,Message>()

    override this.View m dispatch =
        let bag,chat,model = m
        let codeText = bag.Code |> Option.defaultValue ""
        let planText = bag.Plan |> Option.defaultValue ""

        comp<MudPopover> {
            "Open" => TmpState.isIndexOpen chat.Id model
            "AnchorOrigin" => Origin.TopRight
            "TransformOrigin" => Origin.TopRight
            "Paper" => true
            "Class" => "d-flex flex-row flex-grow-1"
            "Style" => "width:80vw; height: 80vh;"
            comp<MudTabs> {
                "Class" => "d-flex flex-1 ma-2 overflow-auto"
                comp<MudTabPanel> {
                    "Text" => "Plan"
                    comp<MudPaper> {
                        "Class" => "d-flex flex-1 ma-2"
                        pre {
                            code {
                                planText
                            }
                        }
                    }
                }
                comp<MudTabPanel> {
                    "Text" => "Code"
                    comp<MudPaper> {
                        "Class" => "d-flex flex-1 ma-2"
                        pre {
                            code {
                                codeText
                            }
                        }
                    }
                }
            }
            comp<MudIconButton> {
                "Class" => "align-self-start ma-2"
                "Icon" => Icons.Material.Outlined.ExpandLess
                on.click(fun _ -> dispatch (Ia_OpenIndex chat.Id))
            }
        }

type WholesaleCodeView() =
    inherit ElmishComponent<Interaction*Model,Message>()

    override this.View mdl dispatch =
        let chat,model = mdl
        comp<MudPaper> {
            "Elevation" => 0
            "Class" => "d-flex flex-none align-center"
            comp<MudIconButton> {
                "Class" => "ma-2 self-align-end d-flex"
                "Icon" => Icons.Material.Outlined.ExpandMore
                "Title" => "Display charts"
                on.click(fun _ -> dispatch (Ia_OpenIndex chat.Id))
            }
        }

type CodeEvalView () =
    inherit ElmishComponent<CodeEvalBag*Interaction*Model,Message>()

    let isMasked (s:string) = s.EndsWith("#")

    override this.View m dispatch =
        let bag,chat,model = m

        comp<MudPaper> {
            "Elevation" => 3
            "class" => "d-flex ma-2"
            ecomp<ChatSettingsView,_,_> (chat,model) dispatch {attr.empty()}
            comp<MudPaper> {
                "Class" => "d-flex flex-1 ma-2 align-self-center "
                ecomp<SystemMessageView,_,_> chat dispatch {attr.empty()}
            }
            ecomp<WholesaleCodeView,_,_> (chat,model) dispatch {attr.empty()}
            ecomp<CodeEvalViewPopup,_,_> (bag,chat,model) dispatch {attr.empty()}
        }

