namespace FsOpenAI.Client
open System
open System.IO
open FSharp.Control
open Microsoft.SemanticKernel
open FsOpenAI.Client.Interactions
open Microsoft.SemanticKernel.Connectors.OpenAI
open Microsoft.SemanticKernel.Text

module DocQnA =
    let saveChunk (id,bytes:byte[]) =
        task {
            let fn = Path.Combine(Path.GetTempPath(),id)
            use str = File.OpenWrite(fn)            
            str.Seek(0L,SeekOrigin.End) |> ignore
            do! str.WriteAsync(bytes)
        }

    let extractDocumentPages (filePath:string) =         
        use doc = UglyToad.PdfPig.PdfDocument.Open(filePath)                
        doc.GetPages() |> Seq.map(fun p -> p.Text) |> Seq.toList

    let extract (id,fileId) dispatch =
        async {
            try
                let fn = Path.Combine(Path.GetTempPath(),fileId)
                extractDocumentPages fn
                |> Seq.iter(fun t ->                     
                    printfn "%A" t
                    dispatch (Srv_Ia_SetContents (id,t,false)))
                dispatch (Srv_Ia_SetContents (id,"",true))
            with ex ->
                dispatch (Srv_Error(ex.Message))
        }

    let getSearchQuery parms modelRefs ch query =
        task {
            let k = (GenUtils.baseKernel parms modelRefs ch).Build()
            let args = GenUtils.kernelArgs ["document",query] (fun x -> x.MaxTokens <- 1000)
            let docQuery = 
                Interaction.getPrompt TemplateType.Extraction ch 
                |> Option.defaultValue Prompts.DocQnA.extractSearchTerms
            let! rslt = k.InvokePromptAsync(docQuery,args) |> Async.AwaitTask
            return rslt.GetValue<string>()
        }

    let extractQuery parms modelsConfig ch dispatch =
        task {
            try 
                let modelRefs = GenUtils.chatModels modelsConfig ch.Parameters.Backend
                let dbag = Interaction.docBag ch
                let document = dbag.Document.DocumentText.Value
                let! query = getSearchQuery parms modelRefs ch document
                dispatch (Srv_Ia_SetSearch(ch.Id,query))
            with ex ->
                dispatch (Srv_Error(ex.Message))
        }

    let summarizeWholeDocument parms modelsConfig ch document dispatch =
        async {
            let! renderedPrompt = GenUtils.renderPrompt Prompts.DocQnA.summarizeDocument (GenUtils.kernelArgsDefault ["input",document]) |> Async.AwaitTask
            let modelRefs = GenUtils.chatModels modelsConfig ch.Parameters.Backend 
            let modelRefs = [GenUtils.optimalModel modelRefs (GenUtils.tokenSize renderedPrompt)]
            let k = (GenUtils.baseKernel parms modelRefs ch).Build()
            let args = GenUtils.kernelArgs ["input",document] (fun x -> x.MaxTokens <- 1000)
            let fn = k.CreateFunctionFromPrompt(Prompts.DocQnA.summarizeDocument)
            let! resp = fn.InvokeAsync(k,args) |> Async.AwaitTask
            return resp.GetValue<string>()
        }

    let summarizeDocumentInChunks parms modelsConfig ch (document:string) dispatch =
        async {
            dispatch (Srv_Ia_Notification(ch.Id,$"Document is large. Summarizing document in chunks..."))
            let chunks =  TextChunker.SplitPlainTextParagraphs(ResizeArray [document],8000,100)
            let! summaries = 
                chunks
                |> Seq.map(fun c -> summarizeWholeDocument parms modelsConfig ch c dispatch)
                |> Async.Parallel
            let summarized = String.Join("\r\n",summaries)
            return summarized
        }

    let summarizeDocument parms modelsConfig ch (document:string) dispatch =
        async {
            let! renderedPrompt = GenUtils.renderPrompt Prompts.DocQnA.summarizeDocument (GenUtils.kernelArgsDefault ["input",document]) |> Async.AwaitTask
            let docTokenSize = GenUtils.tokenSize renderedPrompt
            let tknBudget = GenUtils.tokenBudget modelsConfig ch
            if docTokenSize > tknBudget then 
                return! summarizeDocumentInChunks parms modelsConfig ch document dispatch 
            else
                return! summarizeWholeDocument parms modelsConfig ch document dispatch
        }

    type private Reduction = ReduceDoc | ReduceSearch | ReduceNone

    let private reduceCheck tknsDoc tknsSummary tknsBudget =
        if tknsDoc + tknsSummary < tknsBudget then
            ReduceNone
        else 
            if tknsDoc > tknsSummary then 
                ReduceDoc
            else
                ReduceSearch

    let continueAnswerQuestion parms modelsConfig ch document memories dispatch combinedSearch =
        task {
            let prompt = 
                let dbag = Interaction.docBag ch
                if dbag.DocOnlyQuery then 
                    Prompts.DocQnA.plainDocQuery
                else
                    Interaction.getPrompt DocQuery ch 
                    |> Option.defaultValue Prompts.DocQnA.docQueryWithSearchResults

            let question = Interaction.lastNonEmptyUserMessageText ch
            if Utils.isEmpty question then failwith "no question found"

            let args = 
                [
                    "document",document; 
                    "searchResults",combinedSearch; 
                    "question",question; 
                    "date", DateTime.Now.ToShortDateString()
                ]
            let! renderedPrompt = GenUtils.kernelArgsDefault args |>  GenUtils.renderPrompt prompt
            let ch = Interaction.setUserMessage renderedPrompt ch
            do! Completions.streamCompleteChat parms modelsConfig ch dispatch
        }

    let rec answerQuestion i parms modelsConfig (ch:Interaction) document memories dispatch = 
        task {
            let tknBudget = GenUtils.chatModels modelsConfig ch.Parameters.Backend |> List.map (_.TokenLimit) |> List.max |> float
            let tknsDoc =  GenUtils.tokenSize document
            let combinedSearch = QnA.combineSearchResults tknBudget memories
            let tknsSearch = GenUtils.tokenSize combinedSearch 
            if i < 3 then 
                match reduceCheck tknsDoc tknsSearch tknBudget with 
                | ReduceNone -> do! continueAnswerQuestion parms modelsConfig ch document memories dispatch combinedSearch
                | ReduceSearch -> 
                    dispatch (Srv_Ia_Notification(ch.Id,$"Dropping some search results to meet token limit : {i}"))        
                    let memories = QnA.trimMemories (tknBudget - tknsDoc) memories
                    do! answerQuestion (i+1) parms modelsConfig ch document memories dispatch
                | ReduceDoc -> 
                    dispatch (Srv_Ia_Notification(ch.Id,$"Summarizing document to meet token limit: {i}"))
                    let! smryDoc = summarizeDocument parms modelsConfig ch document dispatch
                    do! answerQuestion (i+1) parms modelsConfig ch smryDoc memories dispatch        
            else 
                do! continueAnswerQuestion parms modelsConfig ch document memories dispatch combinedSearch
        }

    let runDocOnlyPlan (parms:ServiceSettings) modelsConfig (ch:Interaction) dispatch =
        async {
            let dbag = Interaction.docBag ch
            let document =  dbag.Document.DocumentText.Value
            dispatch (Srv_Ia_Notification (ch.Id,$"Querying document only (without involving index search) ..."))
            do! Async.Sleep 100
            do! answerQuestion 1 parms modelsConfig ch document [] dispatch |> Async.AwaitTask
        }

    let runIndexSrchPlan (parms:ServiceSettings) modelsConfig (ch:Interaction) dispatch =
        async {
            let dbag = Interaction.docBag ch
            let document =  dbag.Document.DocumentText.Value
            let cogMems = QnA.chatPdfMemories parms modelsConfig ch   
            let maxDocs = Interaction.maxDocs 1 ch
            let query = 
                if dbag.SearchWithOrigText then 
                    document 
                else                        
                    match dbag.SearchTerms with Some q -> q | _ -> failwith "no search terms found"
            let qMsg = query.Substring(0,min 100 (query.Length-1))                
            dispatch (Srv_Ia_Notification (ch.Id,$"Searching with: {qMsg} ..."))

            let! rephrasedQuestion =
                if ch.Messages.Length > 2 then 
                    dispatch (Srv_Ia_Notification(ch.Id,"Rephrasing question based on chat history ..."))
                    QnA.refineQuery parms modelsConfig ch |> Async.AwaitTask
                else
                    async{return ""}

            if rephrasedQuestion <> "" then 
                dispatch (Srv_Ia_Notification(ch.Id,$"Rephrased question for search: {rephrasedQuestion}" |> Utils.shorten 120))

            let query = query + " " + rephrasedQuestion

            let docs = GenUtils.searchResults parms ch maxDocs query cogMems
            let docs = docs |> List.sortByDescending (fun x->x.Relevance) |> List.truncate maxDocs
            dispatch (Srv_Ia_Notification(ch.Id,$"{docs.Length} query results found. Generating answer..."))
            dispatch (Srv_Ia_SetDocs (ch.Id,docs |> List.map(fun d -> 
                {
                    Text=d.Metadata.Text
                    Embedding=if d.Embedding.HasValue then d.Embedding.Value.ToArray() else [||] 
                    Ref=d.Metadata.ExternalSourceName
                    Title = d.Metadata.Description
                    })))
            do! Async.Sleep 100
            do! answerQuestion 1 parms modelsConfig ch document docs dispatch |> Async.AwaitTask
        }

    let runPlan (parms:ServiceSettings) modelsConfig (ch:Interaction) dispatch =
        async {  
            try
                let dbag = Interaction.docBag ch
                if dbag.DocOnlyQuery then 
                    do! runDocOnlyPlan parms modelsConfig ch dispatch
                else
                    do! runIndexSrchPlan parms modelsConfig ch dispatch
            with ex -> dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }
