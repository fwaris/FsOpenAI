#load "Env.fsx"
(*
Example script that loads a collection of PDF documents in a local folder
to an Azure vector search index
*)

open System
open System.IO
open Azure.AI.OpenAI
open Docnet.Core
open Azure
open Azure.Search.Documents
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Models
open Azure.Search.Documents.Indexes.Models
open FSharp.Control
open Microsoft.SemanticKernel.Text
open FSharp.Data
open Env

let indexName = "gaap";
let srchClient = indexClient.GetSearchClient(indexName)
let PARLELLISM = 2

let newField name = SemanticField(FieldName=name)

let root = @"c:\s\genai\gaap"
let (@@) a b = Path.Combine(a,b)
let docName (s:string) = Path.GetFileName(s)

let docsFiles = Directory.GetFiles(root,"*.pdf") //get all pdfs to shred
let citations = root @@ "citations.csv"          //html link for each pdf document 
                                                 //for hyperlinking from search results view
                                                 //format: Url, document file name without path
let citationsMap =
    CsvFile.Load(citations, hasHeaders=true).Rows
    |> Seq.map(fun r -> r.[1],r.[0])              //hyperlink url, document file name (without path)
    |> Map.ofSeq

let docText (r:Readers.IDocReader) =
    let pages = [for i in 0 .. r.GetPageCount()-1 -> i, r.GetPageReader(i).GetText()]
    pages 
    |> Seq.collect(fun (i,t) -> TextChunker.SplitPlainTextParagraphs(ResizeArray [t],1500,100) |> Seq.map(fun c -> i,c))   
    |> Seq.toList

type Doc = {File:string; Page:int; Chunk:string}

let loadDocs() = 
    docsFiles 
    |> Seq.map(fun x -> docName x,x)
    |> Seq.map(fun (d,fn) -> d, docText (DocLib.Instance.GetDocReader(fn,Models.PageDimensions(1.0))))
    |> Seq.collect(fun (d,txs) -> txs |> Seq.map(fun (i,c) -> {File=d; Page=i; Chunk=c}))

let checkCitations() =
    loadDocs() 
    |> Seq.iter(fun doc -> 
        printfn $"checkign {doc.File}"
        let link = citationsMap.[doc.File]
        ())

checkCitations() //ensure that citations are correct

(*
let ds1 = loadDocs() |> Seq.toArray
File.WriteAllLines(root @@ "alltxt.txt", ds1 |> Seq.map snd)
*)

let extractRetryTime msg = 
    use rdr = new StringReader(msg)
    let readLine (rdr:StringReader) = let l = rdr.ReadLine() in if l <> null then Some (l,rdr) else None
    let lines = rdr |> Seq.unfold readLine
    let retry = lines |> Seq.tryFind(fun l -> l.StartsWith("Retry-After:")) 
    retry |> Option.bind(fun x -> try x.Split(':').[1] |> int |> Some with _ -> None) 

let rec submitLoop<'t> (caller:string) c t : Async<'t> =
    async {
        try
            return! t 
        with ex ->
            match extractRetryTime ex.Message with
            | Some s -> 
                printfn $"request to wait {s} seconds - {caller}"
                do! Async.Sleep (s*1000*PARLELLISM + 5000)
                return! submitLoop caller c t
            | None -> 
                printfn $"{ex.Message}"
                if c < 5 then
                    do! Async.Sleep (PARLELLISM*1000*5)
                    return! submitLoop caller (c+1) t
                else
                    return raise ex
    }


//define the index format
let indexDefinition(name) =     
    let vsconfigName = "my-vector-config"
    let idx = SearchIndex(name)
    idx.VectorSearch <- new VectorSearch()
    idx.VectorSearch.AlgorithmConfigurations.Add (new HnswVectorSearchAlgorithmConfiguration(vsconfigName))
    idx.SemanticSettings <- new SemanticSettings()
    idx.SemanticSettings.Configurations.Add(
        let p1 = PrioritizedFields(TitleField  = newField "title")
        p1.ContentFields.Add(newField "content")
        p1.KeywordFields.Add(newField "category")
 
        let ssM = new SemanticConfiguration(
                    vsconfigName,
                    prioritizedFields = p1)
        ssM)
    let flds : SearchField list = 
        [
            !> SimpleField("id", SearchFieldDataType.String, IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true) 
            !> SearchableField("title", IsFilterable = true, IsSortable = true )
            !> SearchableField("sourcefile", IsFilterable = true, IsSortable = true )
            !> SearchableField("content", IsFilterable = true)

            SearchField("contentVector", 
                ``type`` = SearchFieldDataType.Collection(SearchFieldDataType.Single),
                IsSearchable = true,
                VectorSearchDimensions = modelDimensions,
                VectorSearchConfiguration = vsconfigName
                ) 
            !> SearchableField("category", IsFilterable = true, IsSortable = true, IsFacetable = true)
        ]
    flds |> List.iter idx.Fields.Add
    idx

let loadIndexAsync
    (openAiClient:OpenAIClient) 
    (indexClient:SearchIndexClient)  
    (searchClient:SearchClient)
    (idx:SearchIndex) =
    let docs = loadDocs() |> AsyncSeq.ofSeq

    let indexDocs() = 
        docs
        //|> AsyncSeq.mapAsyncParallelThrottled PARLELLISM (fun (k,tokens) ->
        |> AsyncSeq.mapAsyncParallelRateLimit 7.0 (fun doc ->
            async {
                let t1 = DateTime.Now
                let t = openAiClient.GetEmbeddingsAsync(embModel,EmbeddingsOptions(doc.Chunk)) |> Async.AwaitTask                
                let! emb = submitLoop "embedding" 0 t
                return t1,doc,emb.Value.Data.[0].Embedding |> Seq.toArray            
            })
        |> AsyncSeq.bufferByCountAndTime 100 3000
        |> AsyncSeq.map(fun xs -> 
                let t1 = xs |> Seq.map(fun (t,_,_) -> t) |> Seq.min
                let docs =
                    xs 
                    |> Seq.map(fun (_,doc,embs) ->
                        //printfn "%A" (k,text,embs)
                        let d = SearchDocument()
                        d.["id"] <- Guid.NewGuid()
                        d.["title"] <- $"{doc.File}, Page {doc.Page}"
                        d.["sourcefile"] <- $"{citationsMap.[doc.File]}#page={doc.Page}"
                        d.["content"] <- doc.Chunk
                        d.["contentVector"] <- embs
                        d)
                    |> Seq.toList
                t1,docs)
        //|> AsyncSeq.take 1000
        |> AsyncSeq.iterAsync(fun (t1,docs) -> 
            async {
                try 
                    let batch =  IndexDocumentsBatch.Upload(docs)
                    let t = searchClient.IndexDocumentsAsync(batch) |> Async.AwaitTask                    
                    let! dbatch =  submitLoop "index upload" 0 t
                    let elapsed = (DateTime.Now - t1).TotalSeconds
                    printfn $"[%0.1f{elapsed}]; Added: {dbatch.Value.Results.Count}" 
                    with ex -> 
                        printfn "%A" ex.Message
                ()
            })
    async {
        let! resp = indexClient.DeleteIndexAsync(idx) |> Async.AwaitTask       //delete index
        let! resp = indexClient.CreateOrUpdateIndexAsync(idx) |> Async.AwaitTask
        do! indexDocs()
    }


//load the index
loadIndexAsync openAiClient indexClient srchClient (indexDefinition indexName) |> Async.RunSynchronously
 
