namespace FsOpenAI.Client
open System
open System.IO
open FSharp.Control
open Microsoft.SemanticKernel
open FsOpenAI.Client.Interactions

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

    let summarizePrompt = """[SUMMARIZATION RULES]
DONT WASTE WORDS
USE SHORT, CLEAR, COMPLETE SENTENCES.
DO NOT USE BULLET POINTS OR DASHES.
USE ACTIVE VOICE.
MAXIMIZE DETAIL, MEANING
FOCUS ON THE CONTENT

[BANNED PHRASES]
This article
This document
This page
This material
[END LIST]

Summarize:
Hello how are you?
+++++
Hello

Summarize this
{{$input}}
+++++
"""

    let defaultDocQuery = """
[DOCUMENT]
{{$document}}

Analyze the DOCUMENT and extract information to formulate a search QUERY to extract matching documents from a database. Be sure to include any Accounting Standards like ASC XXX in the query.

DONT GENEARTE SQL. JUST LIST THE TERMS AS COMMA-SEPARATED VALUES

QUERY:
"""

    let queryPrompt parms ch query =
        task {
            let k = (GenUtils.baseKernel parms ch).Build()
            let docQuery = Interaction.getPrompt TemplateType.Extraction ch |> Option.defaultValue defaultDocQuery
            let fn = k.CreateFunctionFromPrompt(docQuery)
            let args = KernelArguments()
            args.["document"] <- query
            let! resp = fn.InvokeAsync(k,args) |> Async.AwaitTask
            return resp.GetValue<string>()
        }

    let extractQuery parms ch dispatch =
        task {
            try 
                let dbag = Interaction.docBag ch
                let document = dbag.Document.DocumentText.Value
                let! query = queryPrompt parms ch document
                dispatch (Srv_Ia_SetSearch(ch.Id,query))
            with ex ->
                dispatch (Srv_Error(ex.Message))
        }

    let defaultDocQueryPrompt = """
DOCUMENT: '''
{{$document}}
'''

SEARCH RESULTS: '''
{{$searchResults}}
'''

Analyze the DOCUMENT  in relation to SEARCH RESULTS for ANSWERING QUESTIONS.
BE BRIEF AND TO THE POINT, BUT WHEN SUPPLYING OPINION, IF YOU SEE THE NEED, YOU CAN BE LONGER.
WHEN ANSWERING QUESTIONS, GIVING YOUR OPINION OR YOUR RECOMMENDATIONS, BE CONTEXTUAL.
If you don't know, ask.
If you are not sure, ask.
Based on calculates from TODAY
TODAY is {{$date}}

QUESTION:'''
{{$question}}
'''

ANSWER:

"""
    let defaultQuestion = "How does the DOCUMENT impact the existing accounting policy in SEARCH RESULTS. Provide a side-by-side comparison"

    let summarizeDocument parms ch (document:string) =
        task {
            let k = (GenUtils.baseKernel parms ch).Build()
            let fn = k.CreateFunctionFromPrompt(summarizePrompt)
            let args = KernelArguments(document)
            let! resp = fn.InvokeAsync(k,args) |> Async.AwaitTask
            return resp.GetValue<string>()
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

    let continueAnswerQuestion parms ch document memories dispatch combinedSearch =
        task {
            let prompt = Interaction.getPrompt DocQuery ch |> Option.defaultValue defaultDocQueryPrompt

            let question = Interaction.lastNonEmptyUserMessageText ch
            let question = if Utils.isEmpty question then defaultQuestion else question

            let k = (GenUtils.baseKernel parms ch).Build()
            let args = KernelArguments()
            args.["document"] <- document
            args.["searchResults"] <- combinedSearch
            args.["question"] <- question
            args.["date"] <- DateTime.Now.ToShortDateString()
            let fn = k.CreateFunctionFromPrompt(prompt)
            let! resp = fn.InvokeAsync(k,args) |> Async.AwaitTask
            let renderedPrompt = resp.GetValue<string>()
            let ch = Interaction.updateAndCloseLastUserMsg renderedPrompt ch
            do! Completions.streamCompleteChat parms ch dispatch
        }

    let rec answerQuestion i parms (ch:Interaction) document memories dispatch = 
        task {
            let tknBudget = GenUtils.safeTokenLimit ch.Parameters.ChatModel                
            let tknsDoc =  GenUtils.tokenSize document
            let combinedSearch = QnA.combineSearchResults tknBudget memories
            let tknsSearch = GenUtils.tokenSize combinedSearch 
            if i < 3 then 
                match reduceCheck tknsDoc tknsSearch tknBudget with 
                | ReduceNone -> do! continueAnswerQuestion parms ch document memories dispatch combinedSearch
                | ReduceSearch -> 
                    dispatch (Srv_Ia_Notification(ch.Id,$"Dropping some search results to meet token limit : {i}"))        
                    let memories = QnA.trimMemories (tknBudget - tknsDoc) memories
                    do! answerQuestion (i+1) parms ch document memories dispatch
                | ReduceDoc -> 
                    dispatch (Srv_Ia_Notification(ch.Id,$"Summarizing document to meet token limit: {i}"))
                    let! smryDoc = summarizeDocument parms ch document
                    do! answerQuestion (i+1) parms ch smryDoc memories dispatch        
            else 
              do! continueAnswerQuestion parms ch document memories dispatch combinedSearch
        }

    let runPlan (parms:ServiceSettings) (ch:Interaction) dispatch =
        async {  
            try
                let dbag = Interaction.docBag ch
                let document =  dbag.Document.DocumentText.Value
                let cogMems = QnA.chatPdfMemories parms ch   
                let maxDocs = Interaction.maxDocs 1 ch
                let query = 
                    if dbag.SearchWithOrigText then 
                        document 
                    else
                        match Interaction.lastSearchQuery ch with Some q -> q | _ -> failwith "no search terms found"
                let qMsg = query.Substring(0,min 100 (query.Length-1))
                dispatch (Srv_Ia_Notification (ch.Id,$"Searching with: {qMsg} ..."))
                let docs = GenUtils.searchResults parms ch maxDocs query cogMems
                let docs = docs |> List.sortBy (fun x->x.Relevance) |> List.truncate maxDocs
                dispatch (Srv_Ia_Notification(ch.Id,$"{docs.Length} query results found. Generating answer..."))
                dispatch (Srv_Ia_SetDocs (ch.Id,docs |> List.map(fun d -> 
                    {
                        Text=d.Metadata.Text
                        Embedding=if d.Embedding.HasValue then d.Embedding.Value.ToArray() else [||] 
                        Ref=d.Metadata.ExternalSourceName
                        Title = d.Metadata.Description
                        })))
                do! Async.Sleep 100
                do! answerQuestion 1 parms ch document docs dispatch |> Async.AwaitTask
            with ex -> dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }
