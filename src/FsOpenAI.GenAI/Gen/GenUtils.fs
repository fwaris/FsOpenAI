namespace FsOpenAI.GenAI
open System
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open FsOpenAI.Vision
open FsOpenAI.GenAI.Models
open FsOpenAI.GenAI.Tokens
open FsOpenAI.GenAI.ChatUtils
open FsOpenAI.GenAI.Endpoints
open FsOpenAI.GenAI.VectorSearch

module GenUtils =
   
    let searchResults embeddingClient mode maxDocs query indexes =
        VectorSearch.search embeddingClient mode maxDocs query indexes

    let toMIdxRefs ch =
            Interaction.getIndexes ch
            |> List.map (function
                | Azure idx -> {Backend="Azure"; Name = idx}
                | Virtual idx -> {Backend="OpenAI"; Name = idx})

    //create diagnostics record for embeddings call
    let diaEntryEmbeddings (ch:Interaction) (invCtx:InvocationContext) model resource query =
        {
            id = Utils.newId()
            UserId = invCtx.User  |> Option.defaultValue C.UNAUTHENTICATED
            AppId = invCtx.AppId |> Option.defaultValue C.DFLT_APP_ID
            Prompt = Embedding query
            Feedback = None
            Question = ch.Question
            Response = ""
            Backend = string ch.Parameters.Backend
            Model = model
            Resource = resource
            IndexRefs = toMIdxRefs ch
            InputTokens = Tokens.tokenSize query |> int
            OutputTokens = 0
            Error = ""
            Timestamp = DateTime.UtcNow
        }

    let private selectEmbeddingModel (invCtx:InvocationContext) backend =
        invCtx.ModelsConfig.EmbeddingsModels
        |> List.tryFind (fun m -> m.Backend = backend)
        |> Option.defaultValue (invCtx.ModelsConfig.EmbeddingsModels.Head)

    ///create diagnostics record for chat completion
    let diaEntryChat (ch:Interaction) (invCtx:InvocationContext) model resource  =
        let prompt = ChatUtils.serializeChat ch
        let inputTokens = Tokens.tokenEstimate ch
        {
            id = Utils.newId()
            UserId = invCtx.User  |> Option.defaultValue C.UNAUTHENTICATED
            AppId = invCtx.AppId |> Option.defaultValue C.DFLT_APP_ID
            Prompt = Chat prompt
            Feedback = None
            Question = ch.Question
            Response = ""
            Backend = string ch.Parameters.Backend
            Model = model
            Resource = resource
            IndexRefs = toMIdxRefs ch
            InputTokens = inputTokens |> int
            OutputTokens = 0
            Error = ""
            Timestamp = DateTime.UtcNow
        }

    //invoke embeddings api to obtain the embedding vector for the given query
    let getEmbeddingGenerator (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) =
        let embModel = selectEmbeddingModel invCtx ch.Parameters.Backend
        Endpoints.getEmbeddingsClient parms ch embModel.Model

    let getEmbeddings (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) query =
        let embModel = selectEmbeddingModel invCtx ch.Parameters.Backend
        let embClient = Endpoints.getEmbeddingsClient parms ch embModel.Model
        let de = diaEntryEmbeddings ch invCtx embModel.Model (string ch.Parameters.Backend) query
        task {
            try
                let! resp = embClient.GenerateAsync([query])
                Monitoring.write (Diag de)
                return resp
            with ex ->
                Env.logException (ex,"getEmbeddings")
                Monitoring.write (Diag {de with Error = ex.Message})
                return raise ex
        }

    let userAgent (invCtx:InvocationContext) =
        invCtx.AppId
        |> Option.map (fun a ->
            invCtx.User
            |> Option.map(fun u -> $"{a}:{u}")
            |> Option.defaultValue a)
        |> Option.defaultValue "fsopenai"

    let extractTripleQuoted (inp:string) =
        let lines =
            seq {
                use sr = new System.IO.StringReader(inp)
                let mutable line = sr.ReadLine()
                while line <> null do
                    yield line
                    line <- sr.ReadLine()
            }
            |> Seq.map(fun x -> x)
            |> Seq.toList
        let addSnip acc accSnip =
            match accSnip with
            |[] -> acc
            | _ -> (List.rev accSnip)::acc
        let isQuote (s:string) = s.StartsWith("```")
        let rec start acc (xs:string list) =
            match xs with
            | []                   -> List.rev acc
            | x::xs when isQuote x -> accQuoted acc [] xs
            | x::xs                -> start acc xs
        and accQuoted acc accSnip xs =
            match xs with
            | []                   -> List.rev (addSnip acc accSnip)
            | x::xs when isQuote x -> start (addSnip acc accSnip) xs
            | x::xs                -> accQuoted acc (x::accSnip) xs
        start [] lines

    let extractCode inp =
        extractTripleQuoted inp
        |> Seq.collect id
        |> fun xs -> String.Join("\n",xs)

    let processImage (parms:ServiceSettings,invCtx:InvocationContext,backend:Backend) (sysMsg:string, userPrompt:string, img:byte[]) =
        match Models.visionModel backend invCtx.ModelsConfig with
        | Some model ->
            async {
                let ep = Endpoints.endpoint parms backend                
                let user = userAgent invCtx
                let imageBytes = img |> System.Convert.ToBase64String
                let imgUri = $"data:image/jpeg;base64,{imageBytes}"
                let chat = [Message("user", [ImageContent(imgUri); TextContent(userPrompt)])]
                let chat = if String.IsNullOrWhiteSpace sysMsg |> not then Message("system", [TextContent(sysMsg)])::chat else chat
                let payload = Payload(chat)
                payload.model <- model.Model
                payload.max_tokens <- 2000
                return! VisionApi.processVision (Uri ep.ENDPOINT) ep.API_KEY user payload |> Async.AwaitTask
            }
        | None -> async { return failwith "No vision model configured" }

    let processVideo (parms:ServiceSettings,invCtx:InvocationContext,backend:Backend) (sysMsg:string, userPrompt:string, frames:byte[] list) =
        match Models.visionModel backend invCtx.ModelsConfig with
        | Some model ->
            async {
                let ep = Endpoints.endpoint parms backend
                let user = userAgent invCtx
                let imageContents =
                    frames
                    |> Seq.map(fun img ->
                        let imageBytes = img |> System.Convert.ToBase64String
                        let imgUri = $"data:image/png;base64,{imageBytes}"
                        ImageContent(imgUri) :> Content)
                    |> List.ofSeq
                let textContent : Content =  TextContent(userPrompt)
                let chat = [Message("user", imageContents @ [textContent])]
                let chat = if String.IsNullOrWhiteSpace sysMsg |> not then Message("system", [TextContent(sysMsg)])::chat else chat
                let payload = Payload(chat)
                payload.model <- model.Model
                payload.max_tokens <- 2000
                return! VisionApi.processVision (Uri ep.ENDPOINT) ep.API_KEY user payload |> Async.AwaitTask
            }
        | None -> async { return failwith "No vision model configured" }
    
    let configExMsg (ex:Exception) =
            if ex.InnerException <> null then
                match ex.InnerException with :? ConfigurationError as cex -> Some cex.Data0 | _ -> None
            else
                None
                
    let handleChatException dispatch chatId sourceText = function
        | ConfigurationError msg ->
            dispatch (Srv_Ia_Done (chatId, Some msg)) 
        | :? AggregateException as ex when (configExMsg ex).IsSome ->
            let msg = (configExMsg ex).Value
            dispatch (Srv_Ia_Done (chatId, Some msg))             
        | ex ->
            Env.logException (ex,"IndexQnA.runPlan")
            dispatch (Srv_Ia_Done (chatId, Some $"Unable to process request."))   
