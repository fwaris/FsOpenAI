﻿namespace FsOpenAI.GenAI
open System
open FSharp.Control
open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.Memory
open Microsoft.SemanticKernel.Connectors.OpenAI
open FsOpenAI.Shared.Interactions
open FsOpenAI.Shared
open FsOpenAI.GenAI.Models
open FsOpenAI.GenAI.Tokens
open FsOpenAI.GenAI.SKernel
open FsOpenAI.GenAI.Endpoints
open FsOpenAI.GenAI.SKernel

module IndexQnA =
    let serializeHistory maxTokens msgs =
        let hist = 
            msgs
            |> List.filter (fun m -> Utils.notEmpty m.Message)
            |> List.map(fun m -> match m.Role with 
                                    | User        -> $"[User]\n{m.Message}" 
                                    | Assistant _ -> $"[Assistant]\n{m.Message}")
            |> List.map(fun t -> t,Tokens.tokenSize t)
            |> List.scan (fun (_,acc) (t,c) -> t,acc + c ) ("",0.)
            |> List.skip 1
            |> List.takeWhile (fun (_,c) -> c < maxTokens)
            |> List.map fst            
        String.Join("\n\n",hist)        

    let trimMemories tknLimit (docs:DocRef seq) =        
        docs 
        |> Seq.sortBy _.Relevance 
        |> Seq.map(fun d -> d,Text.Json.JsonSerializer.Serialize{Id=d.Id;Title=d.Title;Text=d.Text}) |> Seq.toList
        |> Seq.map(fun (d,t) -> d, Tokens.tokenSize t)
        |> Seq.scan (fun (_,acc) (t,c) -> Some t,acc + c ) (None,0.)
        |> Seq.skip 1
        |> Seq.takeWhile (fun (_,c) -> c < tknLimit)
        |> Seq.choose fst
        |> Seq.toList

    let combinedSearch tknLimit (docs:DocRef seq) =
        let docs = trimMemories tknLimit docs
        let sb = Text.StringBuilder()
        docs |> Seq.iter (fun c -> sb.AppendLine(Text.Json.JsonSerializer.Serialize(c)) |> ignore)
        sb.ToString()

    let answerQuestion parms invCtx (ch:Interaction) docs dispatch = 
        task {
            let tknBudget = (Models.getModels ch.Parameters) invCtx ch.Parameters.Backend |> List.map (_.TokenLimit) |> List.max |> float
            let tknsSearch = tknBudget - 500.
            let combinedSearch = combinedSearch tknsSearch docs
            let question = Interaction.lastNonEmptyUserMessageText ch
            let qargs = SKernel.kernelArgsDefault 
                            [ 
                                "question",question; 
                                "date",DateTime.Now.ToShortDateString(); 
                                "documents", combinedSearch
                            ]
            let! prompt = SKernel.renderPrompt Prompts.QnA.questionAnswerPrompt qargs
            let ch = Interaction.setUserMessage prompt ch
            do! Completions.checkStreamCompleteChat parms invCtx ch dispatch None true
        }

    ///semantic memory supporting chatpdf format
    let chatPdfMemories (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) (mode:SemanticVectorSearch.SearchMode): ISemanticTextMemory list =        
        let embModel = invCtx.ModelsConfig.EmbeddingsModels.Head.Model
        let bag = Interaction.qaBag ch |> Option.defaultWith (fun _ -> failwith "no indexes selected")
        if bag.Indexes.IsEmpty then failwith "No indexes selected"
        let realIndexes = bag.Indexes |> List.filter(fun x -> not x.isVirtual)
        if realIndexes.IsEmpty then failwith "No real indexes selected, only virtual indexes found. Re-select index(es))"
        realIndexes
        |> List.map(fun idx -> 
            let idxClient = Indexes.searchServiceClient parms
            let srchClient = idxClient.GetSearchClient(idx.Name)
            let openAIClient,_ = Endpoints.getEmbeddingsClient parms ch embModel
            SemanticVectorSearch.CognitiveSearch(mode,srchClient,openAIClient,["contentVector"],"content","sourcefile","title"))

    type RefinedQuery =         
        {
            searchQuery: string
            searchMode: string
        }

    let fallbackRefineQuery (k:Kernel) userMessage chatHistory = 
        async {
            let args = SKernel.kernelArgsDefault ["question",userMessage; "chatHistory",chatHistory]
            let! rslt = k.InvokePromptAsync(Prompts.QnA.refineQueryFallback,arguments=args) |> Async.AwaitTask
            return rslt.GetValue<string>()
        }

    let runRefineQuery (k:Kernel) userMessage chatHistory = 
        async {
            try
                let args = SKernel.kernelArgsDefault ["question",userMessage; "chatHistory",chatHistory]
                match args.ExecutionSettings.["default"] with 
                | :? OpenAIPromptExecutionSettings as settings -> settings.ResponseFormat  <- typeof<RefinedQuery>
                | _ -> failwith "Unable to set response format for LLM call"
                let! rslt = k.InvokePromptAsync(Prompts.QnA.refineQuery_IdSearchMode,arguments=args) |> Async.AwaitTask
                let resp = rslt.GetValue<OpenAIChatMessageContent>()
                return System.Text.Json.JsonSerializer.Deserialize<RefinedQuery>(resp.Content)                
            with ex -> 
                Env.logError $"Error in runRefineQuery: {ex.Message}"
                let! refinedQuery = fallbackRefineQuery k userMessage chatHistory
                return {searchQuery=refinedQuery;searchMode="Hybrid"}                
        }

    let transform (r:RefinedQuery) =  
        let mode = 
            if r.searchMode.Trim().Equals("Keyword",StringComparison.OrdinalIgnoreCase) then
                SemanticVectorSearch.SearchMode.Plain
            else
               SemanticVectorSearch.SearchMode.Hybrid
        r.searchQuery,mode

    let refineQuery parms modelsConfig (ch:Interaction) = 
        task {
            let modelRefs = (Models.getModels {ch.Parameters with ModelType=MT_Chat}) modelsConfig ch.Parameters.Backend  //use chat model type to refine query
            let nonEmptyMsgs = ch.Messages |> List.rev |> List.skipWhile (fun x-> not x.IsUser)
            let userMessage,historyMessages = List.head nonEmptyMsgs, List.tail nonEmptyMsgs
            let tknBudget =  float modelRefs.Head.TokenLimit - (Tokens.tokenSize userMessage.Message)
            let chatHistory = serializeHistory tknBudget historyMessages
            let tokenSize = 
                Tokens.tokenSize userMessage.Message 
                + Tokens.tokenSize chatHistory 
                + Tokens.tokenSize Prompts.QnA.refineQuery_IdSearchMode
            let modelRef = Models.pick modelRefs
            let k = (SKernel.baseKernel parms [modelRef] ch).Build()
            let! query = runRefineQuery k userMessage.Message chatHistory
            return transform query
        }

    let mapMode chatMode suggestedMode =
        match chatMode with
        | Auto -> suggestedMode
        | Hybrid -> SemanticVectorSearch.SearchMode.Hybrid
        | Keyword -> SemanticVectorSearch.SearchMode.Plain
        | Semantic -> SemanticVectorSearch.SearchMode.Semantic

    let modeLabel = function 
        | SemanticVectorSearch.SearchMode.Semantic -> "Semantic"
        | SemanticVectorSearch.SearchMode.Hybrid -> "Hybrid"
        | SemanticVectorSearch.SearchMode.Plain -> "Keyword"

    let runPlan (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch =
        async {  
            try
                let! query,suggestedMode = refineQuery parms invCtx ch |> Async.AwaitTask
                let chatMode  = Interaction.qaBag ch |> Option.map (fun x -> x.SearchMode) |>  Option.defaultValue SearchMode.Auto
                let mode = mapMode chatMode suggestedMode
                let cogMems = chatPdfMemories parms invCtx ch mode
                let maxDocs = Interaction.maxDocs 1 ch
                dispatch (Srv_Ia_Notification (ch.Id,$"Searching with: {query}"))
                dispatch (Srv_Ia_Notification (ch.Id,$"Search mode: {modeLabel mode}"))               
                let docs = GenUtils.searchResults maxDocs query cogMems
                dispatch (Srv_Ia_Notification(ch.Id,$"{docs.Length} query results found. Generating answer..."))
                dispatch (Srv_Ia_SetDocs (ch.Id,docs))
                do! Async.Sleep 100
                do! answerQuestion parms invCtx ch docs dispatch |> Async.AwaitTask
            with ex ->
                GenUtils.handleChatException dispatch ch.Id "IndexQnA.runPlan" ex
        }

    let answerQuestionTest parms (invCtx:InvocationContext) (ch:Interaction) docs  = 
        task {
            let modelRefs = (Models.getModels ch.Parameters) invCtx ch.Parameters.Backend
            let tknBudget = float modelRefs.Head.TokenLimit
            let tknsSearch = tknBudget - 500.
            let combinedSearch = combinedSearch tknsSearch docs
            let question = Interaction.lastNonEmptyUserMessageText ch
            let qargs = SKernel.kernelArgsDefault 
                            [ 
                                "question",question; 
                                "date",DateTime.Now.ToShortDateString(); 
                                "documents", combinedSearch
                            ]
            let! prompt = SKernel.renderPrompt Prompts.QnA.questionAnswerPrompt qargs
            let ch = Interaction.setUserMessage prompt ch
            let! resp = Completions.completeChat parms invCtx ch (fun _ -> ()) None None 
            return resp.Content
        }

