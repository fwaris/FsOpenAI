namespace FsOpenAI.Shared.Interactions.Core
open FsOpenAI.Shared

module Interactions =
    let private update f id (c:Interaction) = if c.Id = id then f c else c
    let updateWith f id  cs = cs |> List.map(update f id)
    let replace id cs c = cs |> List.map(fun x -> if x.Id = id then c else x)
