module FsOpenAI.GenAI.Models
open FsOpenAI.Shared

[<RequireQualifiedAccess>]
module Models =         

    let pick modelRefs =
        modelRefs
        |> List.tryHead
        |> Option.defaultWith(fun () -> raise (ConfigurationError "No model configured" ))

    let internal chatModels (invCtx:InvocationContext) backend =
        let modelsConfig = invCtx.ModelsConfig
        let modelRefs =
            modelsConfig.ChatModels
            |> List.tryFind (fun m -> m.Backend = backend)
            |> Option.map(fun x -> [x])
            |> Option.defaultValue []
        if modelRefs.IsEmpty then raise (ConfigurationError $"No chat model(s) configured for backend '{backend}'")
        modelRefs

    let getModels (ch:InteractionParameters) invCtx backend = chatModels invCtx backend

    let lowcostModels (invCtx:InvocationContext) backend =
        let modelsConfig = invCtx.ModelsConfig
        let modelRefs =
            modelsConfig.ChatModels
            |> List.tryFind (fun m -> m.Backend = backend)
            |> Option.map(fun x -> [x])
            |> Option.defaultValue []
        if modelRefs.IsEmpty then raise (ConfigurationError $"No lowcost chat model(s) configured for backend '{backend}'") //primarily used for ancilary tasks
        modelRefs
        
    let visionModel (backend:Backend) (modelConfig:ModelsConfig) =
        let filter (m:ModelRef) = if m.Backend = backend then Some m else None
        (modelConfig.ChatModels |> List.choose filter)
        |> List.filter (fun x->x.Model.Contains("-4o"))
        |> List.tryHead
