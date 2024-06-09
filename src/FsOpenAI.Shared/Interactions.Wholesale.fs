namespace FsOpenAI.Shared.Interactions.Wholesale
open System
open FsOpenAI.Shared

module Interaction =
    let code ch = match ch.InteractionType with CodeEval c -> c | _ -> failwith "unexpected chat type"

    let setCode c ch =
        match ch.InteractionType with
        | CodeEval bag -> {ch with InteractionType = CodeEval {bag with Code = c}}
        | _ -> failwith "unexpected chat type"

    let setPlan p ch =
        match ch.InteractionType with
        | CodeEval bag -> {ch with InteractionType = CodeEval {bag with Plan = p}}
        | _ -> failwith "unexpected chat type"

module Interactions =
    open FsOpenAI.Shared.Interactions.Core.Interactions

    let setCode id c cs = updateWith (Interaction.setCode c) id cs
    let setPlan id p cs = updateWith (Interaction.setPlan p) id cs
