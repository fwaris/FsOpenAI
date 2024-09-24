namespace FsOpenAI.GenAI
open System
open FSharp.Control
open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.Memory
open FsOpenAI.Shared.Interactions
open SemanticVectorSearch
open FsOpenAI.Shared
open Microsoft.SemanticKernel.Connectors.OpenAI

module IndexQnA =

    let serializeHistory maxTokens msgs =
        let hist = 
            msgs
            |> List.filter (fun m -> Utils.notEmpty m.Message)
            |> List.map(fun m -> match m.Role with 
                                    | User        -> $"[User]\n{m.Message}" 
                                    | Assistant _ -> $"[Assistant]\n{m.Message}")
            |> List.map(fun t -> t,GenUtils.tokenSize t)
            |> List.scan (fun (_,acc) (t,c) -> t,acc + c ) ("",0.)
            |> List.skip 1
            |> List.takeWhile (fun (_,c) -> c < maxTokens)
            |> List.map fst            
        String.Join("\n\n",hist)        

    let trimMemories tknLimit (docs:MemoryQueryResult seq) =        
        docs
        |> Seq.sortByDescending(fun d->d.Relevance)
        |> Seq.map(fun d -> d, GenUtils.tokenSize d.Metadata.Text)
        |> Seq.scan (fun (_,acc) (t,c) -> Some t,acc + c ) (None,0.)
        |> Seq.skip 1
        |> Seq.takeWhile (fun (_,c) -> c < tknLimit)
        |> Seq.choose fst
        |> Seq.toList

    let combineSearchResults tknLimit (docs:MemoryQueryResult seq) = 
        let docs = trimMemories tknLimit docs 
        let docTexts = docs |> Seq.map(fun d -> d.Metadata.Text)
        String.Join("\r\r", docTexts)

    let answerQuestion parms invCtx (ch:Interaction) docs dispatch = 
        task {
            try
                let tknBudget = GenUtils.chatModels invCtx ch.Parameters.Backend |> List.map (_.TokenLimit) |> List.max |> float
                let tknsSearch = tknBudget - 500.
                let combinedSearch = combineSearchResults tknsSearch docs
                let question = Interaction.lastNonEmptyUserMessageText ch
                let qargs = GenUtils.kernelArgsDefault 
                                [ 
                                    "question",question; 
                                    "date",DateTime.Now.ToShortDateString(); 
                                    "documents", combinedSearch
                                ]
                let! prompt = GenUtils.renderPrompt Prompts.QnA.questionAnswerPrompt qargs
                let ch = Interaction.setUserMessage prompt ch
                do! Completions.streamCompleteChat parms invCtx ch dispatch None
            with ex -> 
                raise ex
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
            let openAIClient,_ = GenUtils.getEmbeddingsClient parms ch embModel
            SemanticVectorSearch.CognitiveSearch(mode,srchClient,openAIClient,["contentVector"],"content","sourcefile","title"))

    type RefinedQuery =         
        {
            searchQuery: string
            searchMode: string
        }

    let fallbackRefineQuery (k:Kernel) userMessage chatHistory = 
        async {
            let args = GenUtils.kernelArgsDefault ["question",userMessage; "chatHistory",chatHistory]
            let! rslt = k.InvokePromptAsync(Prompts.QnA.refineQueryFallback,arguments=args) |> Async.AwaitTask
            return rslt.GetValue<string>()
        }

    let runRefineQuery (k:Kernel) userMessage chatHistory = 
        async {
            try
                let args = GenUtils.kernelArgsDefault ["question",userMessage; "chatHistory",chatHistory]
                match args.ExecutionSettings with 
                | :? OpenAIPromptExecutionSettings as settings -> settings.ResponseFormat  <- "json_object" 
                | _ -> ()
                let! rslt = k.InvokePromptAsync(Prompts.QnA.refineQuery_IdSearchMode,arguments=args) |> Async.AwaitTask
                let resp = rslt.GetValue<OpenAIChatMessageContent>()
                let respStr = GenUtils.extractTripleQuoted resp.Content |> Seq.collect id |> String.concat "\n"
                return System.Text.Json.JsonSerializer.Deserialize<RefinedQuery>(respStr)                
            with ex -> 
                printfn "Error in runRefineQuery: %s" ex.Message
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
            let modelRefs = GenUtils.chatModels modelsConfig ch.Parameters.Backend 
            let nonEmptyMsgs = ch.Messages |> List.rev |> List.skipWhile (fun x-> not x.IsUser)
            let userMessage,historyMessages = List.head nonEmptyMsgs, List.tail nonEmptyMsgs
            let tknBudget =  float modelRefs.Head.TokenLimit - (GenUtils.tokenSize userMessage.Message)
            let chatHistory = serializeHistory tknBudget historyMessages
            let tokenSize = 
                GenUtils.tokenSize userMessage.Message 
                + GenUtils.tokenSize chatHistory 
                + GenUtils.tokenSize Prompts.QnA.refineQuery_IdSearchMode
            let bestModel = GenUtils.optimalModel modelRefs tokenSize
            let k = (GenUtils.baseKernel parms [bestModel] ch).Build()                           
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
                let docs = 
                    cogMems
                    |> AsyncSeq.ofSeq
                    |> AsyncSeq.collect(fun cogMem -> cogMem.SearchAsync("",query,maxDocs) |> AsyncSeq.ofAsyncEnum)
                    |> AsyncSeq.toBlockingSeq
                    |> Seq.toList
                let docs = docs |> List.sortByDescending (fun x->x.Relevance) |> List.truncate maxDocs
                dispatch (Srv_Ia_Notification(ch.Id,$"{docs.Length} query results found. Generating answer..."))
                dispatch (Srv_Ia_SetDocs (ch.Id,docs |> List.map(fun d -> 
                    {
                        Text=d.Metadata.Text
                        Embedding= if d.Embedding.HasValue then d.Embedding.Value.ToArray() else [||] 
                        Ref=d.Metadata.ExternalSourceName
                        Title = d.Metadata.Description
                        })))
                do! Async.Sleep 100
                do! answerQuestion parms invCtx ch docs dispatch |> Async.AwaitTask
            with ex -> dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }

    let answerQuestionTest parms (invCtx:InvocationContext) (ch:Interaction) docs  = 
        task {
            let modelRefs = GenUtils.chatModels invCtx ch.Parameters.Backend
            let tknBudget = float modelRefs.Head.TokenLimit
            let tknsSearch = tknBudget - 500.
            let combinedSearch = combineSearchResults tknsSearch docs
            let question = Interaction.lastNonEmptyUserMessageText ch
            let qargs = GenUtils.kernelArgsDefault 
                            [ 
                                "question",question; 
                                "date",DateTime.Now.ToShortDateString(); 
                                "documents", combinedSearch
                            ]
            let! prompt = GenUtils.renderPrompt Prompts.QnA.questionAnswerPrompt qargs
            let ch = Interaction.setUserMessage prompt ch
            let! resp = Completions.completeChat parms invCtx ch None (fun _ -> ())
            return resp.Content
        }

