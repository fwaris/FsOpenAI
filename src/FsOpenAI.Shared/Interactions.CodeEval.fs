namespace FsOpenAI.Shared.Interactions.CodeEval
open System
open FsOpenAI.Shared

module Interaction =
    let code (ch:Interaction) = ch.Types |> List.tryPick (function CodeEval c -> Some c | _ -> None) 

    let setCode c ch =
        match code ch with
        | Some _ -> {ch with Types = ch.Types |> List.map (function CodeEval bag -> CodeEval {bag with Code = c} | x -> x)}
        | None ->  {ch with Types = (CodeEval {CodeEvalBag.Default with Code=c})::ch.Types}

    let setPlan p ch =
        match code ch with 
        | Some _ -> {ch with Types = ch.Types |> List.map (function CodeEval bag -> CodeEval {bag with Plan = p} | x -> x)}
        | None -> {ch with Types = (CodeEval {CodeEvalBag.Default with Plan=p})::ch.Types}

    let setEvalParms p ch =
        match code ch with 
        | Some _ -> {ch with Types = ch.Types |> List.map (function CodeEval bag -> CodeEval {bag with CodeEvalParms = p} | x -> x)}
        | None -> {ch with Types = (CodeEval {CodeEvalBag.Default with CodeEvalParms=p})::ch.Types}

module Interactions =
    open FsOpenAI.Shared.Interactions.Core.Interactions

    let setCode id c cs = updateWith (Interaction.setCode c) id cs
    let setPlan id p cs = updateWith (Interaction.setPlan p) id cs
    let setEvalParms id p cs = updateWith (Interaction.setEvalParms p) id cs
