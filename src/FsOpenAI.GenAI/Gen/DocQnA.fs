namespace FsOpenAI.GenAI
open System
open System.IO
open FSharp.Control
open Microsoft.SemanticKernel
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open Microsoft.SemanticKernel.Text
open AsyncExts

module DocQnA =
    open System.Data
    open DocumentFormat.OpenXml
    open DocumentFormat.OpenXml.Presentation
    open DocumentFormat.OpenXml.Wordprocessing
    open DocumentFormat.OpenXml.Packaging
    type A = DocumentFormat.OpenXml.Drawing.Text
    open DocumentFormat.OpenXml.Spreadsheet
    open ExcelDataReader
    
    let saveChunk (id,bytes:byte[]) =
        task {
            let fn = Path.Combine(Path.GetTempPath(),id)
            use str = File.OpenWrite(fn)            
            str.Seek(0L,SeekOrigin.End) |> ignore
            do! str.WriteAsync(bytes)
        }

    let extractPdfTexts (filePath:string) =         
        use doc = UglyToad.PdfPig.PdfDocument.Open(filePath)                
        doc.GetPages() |> Seq.map(fun p -> p.Text) |> Seq.toList

    let extractWordTexts (filePath:string) =
        use d = WordprocessingDocument.Open(filePath,false)
        let txt = d.MainDocumentPart.Document.Body.InnerText
        txt
        |> Seq.chunkBySize 1000
        |> Seq.map (fun xs -> String(xs |> Seq.toArray))
        |> Seq.toList

    let extractTextPptx (pptFile:string) = 
        use d = PresentationDocument.Open(pptFile,false)
        let ids = d.PresentationPart.Presentation.SlideIdList
        ids 
        |> Seq.cast<SlideId>
        |> Seq.map(fun (s:SlideId) ->            
            let part = d.PresentationPart.GetPartById(s.RelationshipId) :?> SlidePart
            let xs = 
                part.Slide.Descendants<A>()
                |> Seq.filter(fun t -> t.Text <> null)
                |> Seq.map(fun t -> t.Text)
            String.Join(" ",xs))
        |> Seq.toList

    let extractExcelTexts (filePath:string) =
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        use str = File.OpenRead filePath
        use rdr = ExcelReaderFactory.CreateReader(str)
        while rdr.Read() do rdr.NextResult() |> ignore
        let ds = rdr.AsDataSet()
        seq {
            for t in ds.Tables do
                yield $"# {t.TableName}"
                yield! 
                    t.Rows 
                    |> Seq.cast<DataRow>
                    |> Seq.map(fun r -> 
                        r.ItemArray |> Seq.map(fun x -> x.ToString()) |> String.concat "|")                
            }
        |> Seq.chunkBySize 100
        |> Seq.map (String.concat "\n")
        |> Seq.toList
        
    let extractPlainTexts (filePath:string) =
        seq {
            use str = File.OpenText(filePath)
            while not str.EndOfStream do
                yield str.ReadLine()
        }
        |> Seq.chunkBySize 100
        |> Seq.map(fun rs -> String.Join(" ", rs))
        |> Seq.toList

    let extract (id,fileId,docType) dispatch =
        async {
            try
                let fn = Path.Combine(Path.GetTempPath(),fileId)
                let texts = 
                    match docType with 
                    | None | Some DT_Pdf -> extractPdfTexts fn
                    | Some DT_Word       -> extractWordTexts fn 
                    | Some DT_Powerpoint -> extractTextPptx fn
                    | Some DT_Excel      -> extractExcelTexts fn
                    | Some DT_Text       -> extractPlainTexts fn
                    | Some x             -> failwith $"unsupported document type {x}"
                texts
                |> Seq.iter(fun t -> dispatch (Srv_Ia_SetContents (id,t,false)))
                dispatch (Srv_Ia_SetContents (id,"",true))
            with ex ->
                dispatch (Srv_Error(ex.Message))
        }

    let getSearchQuery parms modelRefs ch query =
        task {
            let query = Utils.shorten 7000 query
            let bestModel = GenUtils.optimalModel modelRefs (GenUtils.tokenSize query)
            let k = (GenUtils.baseKernel parms [bestModel] ch).Build()
            let args = GenUtils.kernelArgs ["document",query] (fun x -> x.MaxTokens <- 1000)
            let docQuery = Prompts.DocQnA.extractSearchTerms
                // Interaction.getPrompt TemplateType.Extraction ch 
                // |> Option.defaultValue Prompts.DocQnA.extractSearchTerms
            let! rslt = k.InvokePromptAsync(docQuery,args) |> Async.AwaitTask
            return rslt.GetValue<string>()
        }

    let extractQuery parms modelsConfig ch dispatch =
        task {
            try 
                let modelRefs = GenUtils.chatModels modelsConfig ch.Parameters.Backend
                let document = 
                    Interaction.docContent ch 
                    |> Option.bind (fun d-> d.DocumentText) 
                    |> Option.defaultWith (fun  _-> failwith "no document found")  
                let! query = getSearchQuery parms modelRefs ch document
                dispatch (Srv_Ia_SetSearch(ch.Id,query))
            with ex ->
                dispatch (Srv_Error(ex.Message))
        }

    let summarizeWholeDocument parms modelsConfig ch document dispatch =
        async {
            let! renderedPrompt = GenUtils.renderPrompt Prompts.DocQnA.summarizeDocument (GenUtils.kernelArgsDefault ["input",document]) |> Async.AwaitTask
            let modelRefs = GenUtils.lowcostModels modelsConfig ch.Parameters.Backend 
            let modelRefs = [GenUtils.optimalModel modelRefs (GenUtils.tokenSize renderedPrompt)]
            let k = (GenUtils.baseKernel parms modelRefs ch).Build()
            let args = GenUtils.kernelArgs ["input",document] (fun x -> x.MaxTokens <- 1000)
            let fn = k.CreateFunctionFromPrompt(Prompts.DocQnA.summarizeDocument)
            let! resp = fn.InvokeAsync(k,args) |> Async.AwaitTask
            return resp.GetValue<string>()
        }

    let summarizeDocumentInChunks parms modelsConfig ch (document:string) dispatch =
        async {
            dispatch (Srv_Ia_Notification(ch.Id,$"Document is large. Summarizing document in chunks.... It may take a while."))
            let chunks =  TextChunker.SplitPlainTextParagraphs(ResizeArray [document],5000,100)
            let summaries = 
                chunks
                |> Seq.map(fun c -> summarizeWholeDocument parms modelsConfig ch c dispatch)
                |> AsyncSeq.ofSeq
                |> AsyncSeq.mapAsyncParallelThrottled 3 id
                |> AsyncSeq.toBlockingSeq
                |> Seq.toList
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
                match ch.Mode with 
                | M_Index                                -> Prompts.DocQnA.plainDocQuery
                | M_Doc                                  -> Prompts.DocQnA.plainDocQuery
                | M_Doc_Index                            -> Prompts.DocQnA.docQueryWithSearchResults
                | _ -> failwith "unexpected chat type for document query"

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
            do! Completions.streamCompleteChat parms modelsConfig ch dispatch None
        }

    let rec answerQuestion i parms invCtx (ch:Interaction) document memories dispatch = 
        task {
            let tknBudget = GenUtils.chatModels invCtx ch.Parameters.Backend |> List.map (_.TokenLimit) |> List.max |> float
            let tknsDoc =  GenUtils.tokenSize document
            let combinedSearch = QnA.combineSearchResults tknBudget memories
            let tknsSearch = GenUtils.tokenSize combinedSearch 
            if i < 3 then 
                match reduceCheck tknsDoc tknsSearch tknBudget with 
                | ReduceNone -> do! continueAnswerQuestion parms invCtx ch document memories dispatch combinedSearch
                | ReduceSearch -> 
                    dispatch (Srv_Ia_Notification(ch.Id,$"Dropping some search results to meet token limit : {i}"))        
                    let memories = QnA.trimMemories (tknBudget - tknsDoc) memories
                    do! answerQuestion (i+1) parms invCtx ch document memories dispatch
                | ReduceDoc -> 
                    dispatch (Srv_Ia_Notification(ch.Id,$"Summarizing document to meet token limit: {i}"))
                    let! smryDoc = summarizeDocument parms invCtx ch document dispatch
                    do! answerQuestion (i+1) parms invCtx ch smryDoc memories dispatch        
            else 
                do! continueAnswerQuestion parms invCtx ch document memories dispatch combinedSearch
        }

    let runDocOnlyPlan (parms:ServiceSettings) modelsConfig (ch:Interaction) dispatch =
        async {
            let document = 
                Interaction.docContent ch 
                |> Option.bind (fun d -> d.DocumentText ) 
                |> Option.defaultWith (fun _ -> failwith "no document found")
            dispatch (Srv_Ia_Notification (ch.Id,$"Document-only mode (no index search) ..."))
            do! Async.Sleep 100
            do! answerQuestion 1 parms modelsConfig ch document [] dispatch |> Async.AwaitTask
        }

    let runIndexSrchPlan (parms:ServiceSettings) modelsConfig (ch:Interaction) dispatch =
        async {
            let document = 
                Interaction.docContent ch 
                |> Option.bind (fun d -> d.DocumentText ) 
                |> Option.defaultWith (fun _ -> failwith "no document found")
            let query = 
                Interaction.docContent ch 
                |> Option.bind (fun d -> d.SearchTerms ) 
                |> Option.defaultWith (fun _ -> failwith "no search terms found")
            let cogMems = QnA.chatPdfMemories parms modelsConfig ch   
            let maxDocs = Interaction.maxDocs 1 ch
            let qMsg = query.Substring(0,min 100 (query.Length-1))  
            dispatch (Srv_Ia_Notification (ch.Id,$"Document + index search mode ..."))
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
                match ch.Mode with 
                | M_Doc -> do! runDocOnlyPlan parms modelsConfig ch dispatch
                | M_Doc_Index -> do! runIndexSrchPlan parms modelsConfig ch dispatch
                | _ -> failwith "unexpected chat mode"
            with ex -> dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }
