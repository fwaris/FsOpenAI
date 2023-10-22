namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open Elmish
open MudBlazor
open FsOpenAI.Client
open FsOpenAI.Client.Interactions
open System.Linq.Expressions
open Microsoft.AspNetCore.Components.Forms


type PromptSelectorView() =
    inherit ElmishComponent<TemplateType*DocBag*Interaction*Model,Message>()
    
    override this.View m dispatch =
        let templateType,dbag,chat,model = m
        let templates = 
            model.templates 
            |> List.tryFind (fun x-> x.Label = dbag.Label)
            |> Option.bind(fun l -> l.Templates |> Map.tryFind templateType)
            |> Option.defaultValue []
        let prompt = Interaction.getPrompt templateType chat |> Option.defaultValue ""
        comp<MudExpansionPanels> {
            "Class" => "d-flex flex-1"
            comp<MudExpansionPanel> {
                "IsInitiallyExpanded" => false
                "Text" => $"Prompt Template [{dbag.Label}]"
                "Dense" => true
                "Color" => Color.Primary
                attr.fragment "TitleContent" (
                    concat {
                        comp<MudStack> {
                            "Row" => true
                            comp<MudText> {"typo" => Typo.h6; text "Prompt Template"}
                            comp<MudText> {
                                "Class" => "d-flex align-self-center"
                                "typo" => Typo.body1
                                "Color" => Color.Tertiary
                                text $"{dbag.Label}"
                            }
                        }
                    }
                )
                comp<MudTable<Template>> {
                    "Style" => "max-height:400px;"
                    "Class" => "ma-2"
                    "FixedHeader" => true
                    "Items" => templates
                    "Dense" => true
                    "Bordered" => true
                    "Elevation" => 3
                    "Outlined" => true
                    attr.fragment "ToolBarContent" (
                        comp<MudText>{
                            "Typo" => Typo.subtitle2
                            "Color" => Color.Tertiary
                            "Available Templates"
                        }
                    )
                    attr.fragment "HeaderContent" (
                        concat {
                            comp<MudTh> {"Name"}
                            comp<MudTh> {"Description"}
                            comp<MudTh> {""}
                        }
                    )
                    attr.fragmentWith "RowTemplate" (fun (t:Template) -> 
                        concat {
                            comp<MudTd> {t.Name }
                            comp<MudTd> {t.Description}
                            comp<MudTd> {
                                comp<MudButton> {
                                    "Color" => Color.Tertiary
                                    on.click (fun _ -> dispatch (Ia_ApplyTemplate (chat.Id,templateType,t)))
                                    text "Set"
                                }
                            }
                        }
                    )                    
                }
                comp<MudTextField<string>> {              
                    "Style" => $"color:{Colors.Green.Darken1};"
                    "Class" => "ma-2 mt-4"
                    attr.callback "ValueChanged" (fun e -> dispatch (Ia_SetPrompt (chat.Id,templateType,e)))
                    "Variant" => Variant.Outlined
                    "Label" => "Prompt Template"
                    "Lines" => 10
                    "Placeholder" => "Prompt template to use for question answering"
                    "Text" => prompt                 
                }                 
            }            
        }
