namespace FsOpenAI.Client
open System
open FSharp.Control
open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.Memory
open FsOpenAI.Client.Interactions

module QnA =

    let history tknLimit msgs =
        let hist = 
            msgs
            |> List.filter (fun m -> Utils.notEmpty m.Message)
            |> List.map(fun m -> match m.Role with 
                                    | User        -> $"[user]:{m.Message}" 
                                    | Assistant _ -> $"[assistant]:{m.Message}")
            |> List.map(fun t -> t,GenUtils.tokenSize t)
            |> List.scan (fun (_,acc) (t,c) -> t,acc + c ) ("",0.)
            |> List.skip 1
            |> List.takeWhile (fun (_,c) -> c < tknLimit)
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

    let answerQuestion parms modelsConfig (ch:Interaction) docs dispatch = 
        task {
            try
                let tknBudget = GenUtils.chatModels modelsConfig ch.Parameters.Backend |> List.map (_.TokenLimit) |> List.max |> float
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
                do! Completions.streamCompleteChat parms modelsConfig ch dispatch
            with ex -> 
                raise ex
        }

    ///semantic memory supporting chatpdf format
    let chatPdfMemories (parms:ServiceSettings) (modelsConfig:ModelsConfig) (ch:Interaction) : ISemanticTextMemory list =
        let embModel = modelsConfig.EmbeddingsModels.Head.Model
        let bag = match ch.InteractionType with QA bag -> bag | DocQA dbag -> dbag.QABag | _ -> failwith "QA interaction expected"        
        if bag.Indexes.IsEmpty then failwith "No indexes selected"
        let realIndexes = bag.Indexes |> List.filter(fun x -> not x.isVirtual)
        if realIndexes.IsEmpty then failwith "No real indexes selected, only virtual indexes found. Re-select index(es))"
        realIndexes
        |> List.map(fun idx -> 
            let idxClient = Indexes.searchServiceClient parms
            let srchClient = idxClient.GetSearchClient(idx.Name)
            let openAIClient = GenUtils.getClient parms ch
            SemanticVectorSearch.CognitiveSearch(bag.HybridSearch,srchClient,openAIClient,embModel,["contentVector"],"content","sourcefile","title"))

    let runRefineQuery (k:Kernel) userMessage chatHistory = 
        async {
            let args = GenUtils.kernelArgsDefault ["question",userMessage; "chatHistory",chatHistory]
            let! rslt = k.InvokePromptAsync(Prompts.QnA.refineQuery,arguments=args) |> Async.AwaitTask
            return rslt.GetValue<string>()
        }

    let refineQuery parms modelsConfig (ch:Interaction) = 
        task {
            let modelRefs = GenUtils.chatModels modelsConfig ch.Parameters.Backend            
            let modelRefs = modelRefs |> List.sortBy (fun x -> x.TokenLimit) |> List.take 1 //can take the cheapest model for refine query
            let k = (GenUtils.baseKernel parms modelRefs ch).Build()                           
            let msgs = ch.Messages |> List.rev |> List.skipWhile (fun x-> not x.IsUser)
            let userMessage = List.head msgs
            let historyMessages = List.tail msgs                
            let tknBudget =  float modelRefs.Head.TokenLimit - (GenUtils.tokenSize userMessage.Message)
            let chatHistory = history tknBudget historyMessages
            printfn "Chat History: %A" chatHistory
            let! query = runRefineQuery k userMessage.Message chatHistory
            return query
        }

    let runPlan (parms:ServiceSettings) modelsConfig (ch:Interaction) dispatch =
        async {  
            try
                let cogMems = chatPdfMemories parms modelsConfig ch   
                let maxDocs = Interaction.maxDocs 1 ch              
                let! query = refineQuery parms modelsConfig ch |> Async.AwaitTask
                dispatch (Srv_Ia_Notification (ch.Id,$"Searching with: {query}"))
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
                do! answerQuestion parms modelsConfig ch docs dispatch |> Async.AwaitTask
            with ex -> dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }

    let answerQuestionTest parms modelsConfig (ch:Interaction) docs  = 
        task {
            let modelRefs = GenUtils.chatModels modelsConfig ch.Parameters.Backend
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
            let! xs =  Completions.streamChat parms modelRefs ch  
            let! acc = xs |> AsyncSeq.fold (fun acc (i,x) -> x::acc) [] 
            return String.Join("", List.rev acc)
        }

