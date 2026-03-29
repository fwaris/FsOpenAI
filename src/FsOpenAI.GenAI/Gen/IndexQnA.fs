namespace FsOpenAI.GenAI
open System
open FSharp.Control
open FsOpenAI.Shared.Interactions
open FsOpenAI.Shared
open FsOpenAI.GenAI.Models
open FsOpenAI.GenAI.Tokens
open FsOpenAI.GenAI.SKernel
open FsOpenAI.GenAI.VectorSearch

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
            do! Completions.checkStreamCompleteChat parms invCtx ch dispatch None 
        }

    ///Search collections backing chatpdf indexes
    let chatPdfSearchIndexes (parms:ServiceSettings) (ch:Interaction) : SearchIndex list =
        let bag = Interaction.qaBag ch |> Option.defaultWith (fun _ -> failwith "no indexes selected")
        if bag.Indexes.IsEmpty then failwith "No indexes selected"
        let realIndexes = bag.Indexes |> List.filter(fun x -> not x.isVirtual)
        if realIndexes.IsEmpty then failwith "No real indexes selected, only virtual indexes found. Re-select index(es))"
        let idxClient = Indexes.searchServiceClient parms
        realIndexes
        |> List.map(fun idx -> VectorSearch.createSearchIndex idxClient idx.Name)

    type RefinedQuery =         
        {
            searchQuery: string
            searchMode: string
        }

    let private renderPrompt prompt args =
        SKernel.kernelArgsDefault args
        |> SKernel.renderPrompt prompt

    let private completePromptWithModel parms invCtx ch modelRef prompt args maxTokens responseFormat =
        async {
            let! renderedPrompt = renderPrompt prompt args |> Async.AwaitTask
            let modelSelector (_:InvocationContext) (_:Backend) = [modelRef]
            return! Completions.completePrompt parms invCtx ch (fun _ -> ()) (Some modelSelector) renderedPrompt maxTokens responseFormat
        }

    let fallbackRefineQuery parms invCtx ch modelRef userMessage chatHistory = 
        async {
            let! rslt =
                completePromptWithModel parms invCtx ch modelRef Prompts.QnA.refineQueryFallback ["question",userMessage; "chatHistory",chatHistory] None None
            return Completions.responseText rslt
        }

    let runRefineQuery parms invCtx ch modelRef userMessage chatHistory = 
        async {
            try
                let! rslt =
                    completePromptWithModel parms invCtx ch modelRef Prompts.QnA.refineQuery_IdSearchMode ["question",userMessage; "chatHistory",chatHistory] None (Some typeof<RefinedQuery>)
                let resp = Completions.responseText rslt
                return System.Text.Json.JsonSerializer.Deserialize<RefinedQuery>(resp)                
            with ex -> 
                Env.logError $"Error in runRefineQuery: {ex.Message}"
                let! refinedQuery = fallbackRefineQuery parms invCtx ch modelRef userMessage chatHistory
                return {searchQuery=refinedQuery;searchMode="Hybrid"}                
        }

    let transform (r:RefinedQuery) =  
        let mode = 
            if r.searchMode.Trim().Equals("Keyword",StringComparison.OrdinalIgnoreCase) then
                VectorSearch.SearchMode.Plain
            else
               VectorSearch.SearchMode.Hybrid
        r.searchQuery,mode

    let refineQuery parms invCtx (ch:Interaction) = 
        task {
            let modelRefs = Models.getModels ch.Parameters invCtx ch.Parameters.Backend  //use chat model type to refine query
            let nonEmptyMsgs = ch.Messages |> List.rev |> List.skipWhile (fun x-> not x.IsUser)
            let userMessage,historyMessages = List.head nonEmptyMsgs, List.tail nonEmptyMsgs
            let tknBudget =  float modelRefs.Head.TokenLimit - (Tokens.tokenSize userMessage.Message)
            let chatHistory = serializeHistory tknBudget historyMessages
            let modelRef = Models.pick modelRefs
            let! query = runRefineQuery parms invCtx ch modelRef userMessage.Message chatHistory
            return transform query
        }

    let mapMode (chatMode:FsOpenAI.Shared.SearchMode) (suggestedMode:VectorSearch.SearchMode) : VectorSearch.SearchMode =
        match chatMode with
        | FsOpenAI.Shared.SearchMode.Auto -> suggestedMode
        | FsOpenAI.Shared.SearchMode.Hybrid -> VectorSearch.SearchMode.Hybrid
        | FsOpenAI.Shared.SearchMode.Keyword -> VectorSearch.SearchMode.Plain
        | FsOpenAI.Shared.SearchMode.Semantic -> VectorSearch.SearchMode.Semantic

    let modeLabel = function 
        | VectorSearch.SearchMode.Semantic -> "Semantic"
        | VectorSearch.SearchMode.Hybrid -> "Hybrid"
        | VectorSearch.SearchMode.Plain -> "Keyword"

    let runPlan (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch =
        async {  
            try
                let! query,suggestedMode = refineQuery parms invCtx ch |> Async.AwaitTask
                let chatMode  = Interaction.qaBag ch |> Option.map (fun x -> x.SearchMode) |>  Option.defaultValue SearchMode.Auto
                let mode = mapMode chatMode suggestedMode
                let searchIndexes = chatPdfSearchIndexes parms ch
                let embeddingClient = GenUtils.getEmbeddingGenerator parms invCtx ch
                let maxDocs = Interaction.maxDocs 1 ch
                dispatch (Srv_Ia_Notification (ch.Id,$"Searching with: {query}"))
                dispatch (Srv_Ia_Notification (ch.Id,$"Search mode: {modeLabel mode}"))               
                let! docs = GenUtils.searchResults embeddingClient mode maxDocs query searchIndexes
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
            return Completions.responseText resp
        }
