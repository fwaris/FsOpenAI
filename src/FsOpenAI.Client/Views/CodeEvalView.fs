namespace FsOpenAI.Client.Views
open System
open System.IO
open Bolero
open Bolero.Html
open Elmish
open Radzen
open Radzen.Blazor
open FsOpenAI.Client
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions.CodeEval

type CodeEvalView () =
    inherit ElmishComponent<CodeEvalBag*Interaction*Model,Message>()

    let isMasked (s:string) = s.EndsWith("#")

    override this.View m dispatch =
        let bag,chat,model = m
        div {
            bag.Code |> Option.defaultValue ""
        }
