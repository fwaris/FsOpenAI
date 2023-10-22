#load "packages.fsx"
open System
open System.Threading
open System.Threading.Tasks
open Azure
open Azure.Search.Documents.Indexes
open Azure.AI.OpenAI
open FSharp.Control
open FsOpenAI.Client
open System.IO
open Azure.Search.Documents.Indexes.Models
open Azure.Search.Documents.Models
open Azure.Search.Documents
open Microsoft.SemanticKernel.Text
open FSharp.Data
open Docnet.Core

let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

let runT t = t |> Async.AwaitTask |> Async.RunSynchronously
let runA a = a |> Async.RunSynchronously

let modelDimensions = 1536; //dimensions for gpt3 based embedding model
let rng = Random()
let randSelect (ls:_ list) = ls.[rng.Next(ls.Length)]

let defaultSettings() = 
    {
        AZURE_OPENAI_ENDPOINTS = []
        AZURE_SEARCH_ENDPOINTS = []
        AZURE_OPENAI_MODELS = None
        BING_ENDPOINT = None
        OPENAI_MODELS = Some(
            {
                CHAT = ["gpt-3.5-turbo-16k"; "gpt-3.5-turbo"; "gpt-4"]
                COMPLETION = ["text-davinci-003"]
                EMBEDDING = ["text-embedding-ada-002"]
            }
        )
        OPENAI_KEY = None
    }

let defaultSettingsFile = 
    let fn = "%USERPROFILE%/.fsopenai/ServiceSettings.json"
    Environment.ExpandEnvironmentVariables(fn)

let settings = ref (defaultSettings())

let installSettings (file:string) = 
    let fn = Environment.ExpandEnvironmentVariables(file)
    let sttngs = System.Text.Json.JsonSerializer.Deserialize<FsOpenAI.Client.ServiceSettings>(System.IO.File.ReadAllText fn)
    settings.Value <- sttngs

do installSettings defaultSettingsFile
    
let openAIEndpoint() = randSelect settings.Value.AZURE_OPENAI_ENDPOINTS
let searchEndpoint() = settings.Value.AZURE_SEARCH_ENDPOINTS.Head

let indexClient() = 
    let ep = searchEndpoint()
    SearchIndexClient(Uri ep.ENDPOINT,AzureKeyCredential(ep.API_KEY))

let openAiClient() = 
    let ep = openAIEndpoint()
    let openAiEndpoint = $"https://{ep.RESOURCE_GROUP}.openai.azure.com"
    OpenAIClient(Uri openAiEndpoint,AzureKeyCredential(ep.API_KEY))

let inline await fn = fn |> Async.AwaitTask |> Async.RunSynchronously

let inline awaitList asyncEnum = asyncEnum |> AsyncSeq.ofAsyncEnum |> AsyncSeq.toBlockingSeq |> Seq.toList

let asyncShow a = 
    async {
        let! (r:Microsoft.SemanticKernel.Orchestration.SKContext) = a
        printfn "Answer:"
        printfn "%A" r.Variables.Input
    }
    |> Async.Start

module Async =
   let map f a = async.Bind(a, f >> async.Return)

module AsyncSeq =
    let mapAsyncParallelThrottled (parallelism:int) (f:'a -> Async<'b>) (s:AsyncSeq<'a>) : AsyncSeq<'b> = asyncSeq {
        use mb = MailboxProcessor.Start (ignore >> async.Return)
        use sm = new SemaphoreSlim(parallelism)
        let! err =
            s
            |> AsyncSeq.iterAsync (fun a -> async {
            let! _ = sm.WaitAsync () |> Async.AwaitTask
            let! b = Async.StartChild (async {
                try return! f a
                finally sm.Release () |> ignore })
            mb.Post (Some b) })
            |> Async.map (fun _ -> mb.Post None)
            |> Async.StartChildAsTask
        yield!
            AsyncSeq.unfoldAsync (fun (t:Task) -> async{
            if t.IsFaulted then 
                return None
            else 
                let! d = mb.Receive()
                match d with
                | Some c -> 
                    let! d' = c
                    return Some (d',t)
                | None -> return None
            })
            err
    }
(* 
      //implementation possible within AsyncSeq, with the supporting code available there       
      let mapAsyncParallelThrottled (parallelism:int) (f:'a -> Async<'b>) (s:AsyncSeq<'a>) : AsyncSeq<'b> = asyncSeq {
        use mb = MailboxProcessor.Start (ignore >> async.Return)
        use sm = new SemaphoreSlim(parallelism)
        let! err =
          s
          |> iterAsync (fun a -> async {
            do! sm.WaitAsync () |> Async.awaitTaskUnitCancellationAsError
            let! b = Async.StartChild (async {
              try return! f a
              finally sm.Release () |> ignore })
            mb.Post (Some b) })
          |> Async.map (fun _ -> mb.Post None)
          |> Async.StartChildAsTask
        yield!
          replicateUntilNoneAsync (Task.chooseTask (err |> Task.taskFault) (async.Delay mb.Receive))
          |> mapAsync id }
*)
    let mapAsyncParallelRateLimit (opsPerSecond:float) (f:'a -> Async<'b>) (s:AsyncSeq<'a>) : AsyncSeq<'b> = asyncSeq {
        let mutable tRef = DateTime.Now
        use mb = MailboxProcessor.Start (ignore >> async.Return)
        let mutable l = 0L
        let incr() = Interlocked.Increment(&l)
        let! err =
            s
            |> AsyncSeq.iterAsync (fun a -> async {
                let l' = incr() |> float
                let elapsed = (DateTime.Now - tRef).TotalSeconds 
                let rate = l' / elapsed |> min (2.0 * opsPerSecond)
                if elapsed > 60. then
                    tRef <- DateTime.Now
                    l <- 0
                    printfn $"rate {rate}"
                let diffRate = rate - opsPerSecond
                if diffRate > 0 then
                    do! Async.Sleep (int diffRate * 1000)
                let! b = Async.StartChild (async {                    
                    try return! f a
                    finally () })
                mb.Post (Some b) })
            |> Async.map (fun _ -> mb.Post None)
            |> Async.StartChildAsTask
        yield!
            AsyncSeq.unfoldAsync (fun (t:Task) -> async{
            if t.IsFaulted then 
                return None
            else 
                let! d = mb.Receive()
                match d with
                | Some c -> 
                    let! d' = c
                    return Some (d',t)
                | None -> return None
            })
            err
    }

let collectString (rs:AsyncSeq<Nullable<int>*ChatMessage>) =
    let mutable ls = []
    rs
    //|> asAsyncSeqRe
    |> AsyncSeq.map(fun(i,x) -> x)
    |> AsyncSeq.iter(fun x-> printfn "%A" x.Content; ls<-x.Content::ls)
    |> Async.RunSynchronously
    List.rev ls

module Index = 
    let WAIT_MULTIPLIER = 2

    let newField name = SemanticField(FieldName=name)

    type Doc = {File:string; Page:int; Chunk:string; Link:string}
    type DocEmb = {Time:DateTime; Doc:Doc; Embeddings:float32[]}

        //define the index format
    let indexDefinition(name) =     
        let vsconfigName = "my-vector-config"
        let idx = SearchIndex(name)
        idx.VectorSearch <- new VectorSearch()
        idx.VectorSearch.Algorithms.Add (new HnswVectorSearchAlgorithmConfiguration(vsconfigName))
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
                    VectorSearchProfile = vsconfigName
                    ) 
                !> SearchableField("category", IsFilterable = true, IsSortable = true, IsFacetable = true)
            ]
        flds |> List.iter idx.Fields.Add
        idx


    let toSearchDoc (docEmb:DocEmb) =
        let doc = docEmb.Doc
        let d = SearchDocument()
        d.["id"] <- Guid.NewGuid()
        d.["title"] <- $"{doc.File}, Page {doc.Page}"
        d.["sourcefile"] <- doc.Link
        d.["content"] <- doc.Chunk
        d.["contentVector"] <- docEmb.Embeddings
        d

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
                    do! Async.Sleep (s*1000*WAIT_MULTIPLIER + 5000)
                    return! submitLoop caller c t
                | None -> 
                    printfn $"{ex.Message}"
                    if c < 5 then
                        do! Async.Sleep (WAIT_MULTIPLIER*1000*5)
                        return! submitLoop caller (c+1) t
                    else
                        return raise ex
        }

    let loadIndexAsync
        (idx:SearchIndex) 
        (docs:AsyncSeq<SearchDocument>)
        =
        let indexDocs (searchClient:SearchClient) = 
            docs            
            |> AsyncSeq.bufferByCountAndTime 100 3000
            |> AsyncSeq.iterAsync(fun docs -> 
                async {
                    try 
                        let batch =  IndexDocumentsBatch.Upload(docs)
                        let t = searchClient.IndexDocumentsAsync(batch) |> Async.AwaitTask                    
                        let! dbatch =  submitLoop "index upload" 0 t
                        printfn $"Added: {dbatch.Value.Results.Count}" 
                        with ex -> 
                            printfn "%A" ex.Message
                    ()
                })
        async {
            let indexClient = indexClient()
            let searchClient = indexClient.GetSearchClient(idx.Name)
            let! resp = indexClient.DeleteIndexAsync(idx) |> Async.AwaitTask       //delete index
            let! resp = indexClient.CreateOrUpdateIndexAsync(idx) |> Async.AwaitTask
            do! indexDocs searchClient
            printfn "done loading index"
        }

    let getEmbeddingsAsync rateLimit (chunks : AsyncSeq<Doc>) =
        chunks
        |> AsyncSeq.mapAsyncParallelRateLimit rateLimit (fun doc ->
            async {
                let t1 = DateTime.Now
                let client = openAiClient()
                let t = client.GetEmbeddingsAsync(settings.Value.AZURE_OPENAI_MODELS.Value.EMBEDDING.Head,EmbeddingsOptions(doc.Chunk)) |> Async.AwaitTask                
                let! emb = submitLoop "embedding" 0 t
                return {Time = t1; Doc=doc; Embeddings = emb.Value.Data.[0].Embedding |> Seq.toArray }
            })

    let shredPdfsAsync (folder:string) =
        if Directory.Exists folder |> not then failwith $"folder {folder} does not exist"
        let root = folder
        let (@@) a b = Path.Combine(a,b)
        let docName (s:string) = Path.GetFileName(s)

        let docsFiles = Directory.GetFiles(root,"*.pdf") //get all pdfs to shred
        let citations = root @@ "citations.csv"          //html link for each pdf document 
                                                         //for hyperlinking from search results view
                                                         //format: Url, document file name without path
        if File.Exists citations |> not then failwith $"Citations file not found at {citations}"            
        
        let citationsMap =
            CsvFile.Load(citations, hasHeaders=true).Rows
            |> Seq.map(fun r -> r.[1],r.[0])              //hyperlink url, document file name (without path)
            |> Map.ofSeq

        let docText (r:Readers.IDocReader) =
            let pages = [for i in 0 .. r.GetPageCount()-1 -> i, r.GetPageReader(i).GetText()]
            pages 
            |> Seq.collect(fun (i,t) -> TextChunker.SplitPlainTextParagraphs(ResizeArray [t],1500,100) |> Seq.map(fun c -> i,c))   
            |> Seq.toList

        let readDocs() = 
            docsFiles 
            |> Seq.map(fun x -> docName x,x)
            |> Seq.map(fun (d,fn) -> d, docText (DocLib.Instance.GetDocReader(fn,Models.PageDimensions(1.0))))
            |> Seq.collect(fun (d,txs) -> txs |> Seq.map(fun (i,c) -> 
                let baseLink = citationsMap.[d]
                let link = $"{baseLink}#page={i}"
                {File=d; Page=i; Chunk=c; Link=link}))
            |> Seq.filter(fun d-> d.Chunk.Trim().Length > 0)

        let checkCitations() =
            docsFiles
            |> Seq.map docName
            |> Seq.iter(fun doc -> 
                printfn $"checking citation for {doc}"
                let link = citationsMap.[doc]
                ())
        checkCitations() //ensure that citations are correct
        readDocs() |> AsyncSeq.ofSeq
