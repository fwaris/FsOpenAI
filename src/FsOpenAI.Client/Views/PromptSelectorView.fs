namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
()

// type PromptViewPopup() =
//     inherit ElmishComponent<TemplateType*DocBag*Interaction*Model,Message>()
//     override this.View m dispatch =
//         let templateType,dbag,chat,model = m
//         let templates = 
//             model.templates 
//             |> List.tryFind (fun x-> x.Label = dbag.Label)
//             |> Option.bind(fun l -> l.Templates |> Map.tryFind templateType)
//             |> Option.defaultValue []
//         let prompt = Interaction.getPrompt templateType chat |> Option.defaultValue ""
//         comp<MudPopover> {
//             "Open" => TmpState.isPromptsOpen chat.Id model
//             "Style" => "max-width:85%; overflow:auto"
//             "AnchorOrigin" => Origin.TopRight
//             "TransformOrigin" => Origin.TopRight
//             "Paper" => true
//             "Class" => "d-flex flex-row flex-grow-1 align-start"
//             comp<MudPaper> {
//                 "Class" => "d-flex flex-column flex-grow-1"
//                 comp<MudTable<Template>> {
//                     "Style" => "max-height:400px;"
//                     "Class" => "ma-2 d-flex flex-grow-1"
//                     "FixedHeader" => true
//                     "Items" => templates
//                     "Dense" => true
//                     "Bordered" => true
//                     "Elevation" => 3
//                     "Outlined" => true
//                     attr.fragment "ToolBarContent" (
//                         comp<MudText>{
//                             "Typo" => Typo.subtitle2
//                             "Color" => Color.Tertiary
//                             "Available Templates"
//                         }
//                     )
//                     attr.fragment "HeaderContent" (
//                         concat {
//                             comp<MudTh> {"Name"}
//                             comp<MudTh> {"Description"}
//                             comp<MudTh> {""}
//                         }
//                     )
//                     attr.fragmentWith "RowTemplate" (fun (t:Template) -> 
//                         concat {
//                             comp<MudTd> {t.Name }
//                             comp<MudTd> {t.Description}
//                             comp<MudTd> {
//                                 comp<MudButton> {
//                                     "Color" => Color.Tertiary
//                                     on.click (fun _ -> dispatch (Ia_ApplyTemplate (chat.Id,templateType,t)))
//                                     text "Set"
//                                 }
//                             }
//                         }
//                     )                    
//                 }
//                 comp<MudTextField<string>> {              
//                     "Style" => $"color:{Colors.Green.Darken1};"
//                     "Class" => "ma-2 mt-4 d-flex flex-grow-1"
//                     attr.callback "ValueChanged" (fun e -> dispatch (Ia_SetPrompt (chat.Id,templateType,e)))
//                     "Variant" => Variant.Outlined
//                     "Label" => "Prompt Template"
//                     "Lines" => 10
//                     "Placeholder" => "Prompt template to use for question answering"
//                     "Text" => prompt                 
//                 }                 
//             }
//             comp<MudPaper> {
//                 "Elevation" => 0
//                 comp<MudIconButton> {
//                     "Icon" => Icons.Material.Outlined.ExpandLess
//                     on.click (fun _ -> dispatch (Ia_TogglePrompts chat.Id))
//                 }
//         }
//     }                

// type PromptSelectorView() =
//     inherit ElmishComponent<TemplateType*DocBag*Interaction*Model,Message>()
    
//     override this.View m dispatch =
//         let templateType,dbag,chat,model = m
//         comp<MudPaper> {
//             "Class" => "d-flex flex-grow-1 flex-row ma-2"
//             "Elevation" => 0
//             comp<MudBreakpointProvider> {
//                 comp<MudHidden> {
//                     "Breakpoint" => Breakpoint.SmAndDown
//                     comp<MudText> {
//                         "Class" => "d-flex align-self-center"
//                         text "Templates:"
//                         }
//                 }
//             }
//             comp<MudText> {
//                 "Class" => "d-flex align-self-center ma-1 ml-2"
//                 "typo" => Typo.body1
//                 "Color" => Color.Tertiary
//                 text $"{dbag.Label}"
//             }
//             comp<MudSpacer> {attr.empty()}
//             comp<MudPaper> {
//                 "Class" => "d-flex align-self-start ma-1"
//                 "Elevation" => 0
//                 comp<MudIconButton> {
//                     "Icon" => Icons.Material.Outlined.ExpandMore
//                     "Title" => "Show prompt templates"
//                     on.click (fun _ -> dispatch (Ia_TogglePrompts chat.Id))
//                 }
//             }
//             ecomp<PromptViewPopup,_,_> (templateType,dbag,chat,model) dispatch {attr.empty()}
//         }
