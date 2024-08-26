namespace FsOpenAI.Shared.Interactions.Wholesale
open System
open FsOpenAI.Shared

module Interaction =
    let code (ch:Interaction) = ch.Types |> List.tryPick (function CodeEval c -> Some c | _ -> None) |> Option.defaultWith (fun _ -> failwith "unexpected chat type")

    let setCode c ch =
        {ch with Types = ch.Types |> List.map (function CodeEval bag -> CodeEval {bag with Code = c} | x -> x)}

    let setPlan p ch =
        {ch with Types = ch.Types |> List.map (function CodeEval bag -> CodeEval {bag with Plan = p} | x -> x)}

module Interactions =
    open FsOpenAI.Shared.Interactions.Core.Interactions

    let setCode id c cs = updateWith (Interaction.setCode c) id cs
    let setPlan id p cs = updateWith (Interaction.setPlan p) id cs
