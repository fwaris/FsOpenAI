#load "packages.fsx"
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Text.Json
open Azure
open Azure.AI.OpenAI
open Azure.Search.Documents
open Azure.Search.Documents.Indexes
open Microsoft.SemanticKernel.Text
open FSharp.Control
open FsOpenAI.Shared
open FSharp.Data
open Docnet.Core

let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

let (@@) (a:string) (b:string) = Path.Combine(a,b)

let runT t = t |> Async.AwaitTask |> Async.RunSynchronously
let runA a = a |> Async.RunSynchronously

let modelDimensions = 1536; //dimensions for gpt3 based embedding model
let rng = Random()
let randSelect (ls:_ list) = ls.[rng.Next(ls.Length)]

let defaultSettings() =
    {
        AZURE_OPENAI_ENDPOINTS = []
        AZURE_SEARCH_ENDPOINTS = []
        EMBEDDING_ENDPOINTS = []
        BING_ENDPOINT = None
        OPENAI_KEY = None
        LOG_CONN_STR = None
    }

let expandEnv (s:string) = Environment.ExpandEnvironmentVariables(s)

let defaultSettingsFile =
    let fn = "%USERPROFILE%/.fsopenai/ServiceSettings.json"
    expandEnv fn

let settings = ref (defaultSettings())

let installSettings (file:string) =
    let fn = Environment.ExpandEnvironmentVariables(file)
    let sttngs = System.Text.Json.JsonSerializer.Deserialize<ServiceSettings>(System.IO.File.ReadAllText fn, Utils.serOptions())
    settings.Value <- sttngs

do if File.Exists defaultSettingsFile then 
    try 
        installSettings defaultSettingsFile
    with ex -> 
        printfn $"Error loading default settings file {defaultSettingsFile}. \n {ex.StackTrace}"

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

// let collectString (rs:AsyncSeq<Nullable<int>*ChatMessage>) =
//     let mutable ls = []
//     rs
//     //|> asAsyncSeqRe
//     |> AsyncSeq.map(fun(i,x) -> x)
//     |> AsyncSeq.iter(fun x-> printfn "%A" x.Content; ls<-x.Content::ls)
//     |> Async.RunSynchronously
//     List.rev ls

module Secrets =
    open Azure.Identity;
    open Azure.Security.KeyVault.Secrets

    let KeyVault = System.Environment.GetEnvironmentVariable(C.FSOPENAI_AZURE_KEYVAULT)

    let kvUri (keyVault:string) = $"https://{keyVault}.vault.azure.net";

    let setCreds keyVault keyName settingsFile =
        let settingsFile  = Environment.ExpandEnvironmentVariables(settingsFile)
        let txt = File.ReadAllText settingsFile
        let txtArr = System.Text.UTF8Encoding.Default.GetBytes(txt)
        let txt64 = System.Convert.ToBase64String(txtArr)
        let kvUri = kvUri keyVault
        let c = new DefaultAzureCredential()
        let client = new SecretClient(new Uri(kvUri), c);
        let r = client.SetSecret(keyName,txt64)
        printfn "%A" r
        printfn $"set {keyVault}/{keyName} to {settingsFile}"

    let getCreds keyVault keyName =
        printfn "getting ..."
        let kvUri = kvUri keyVault
        let c = new DefaultAzureCredential()
        let client = new SecretClient(new Uri(kvUri), c);
        let r = client.GetSecret(keyName)
        let txt64 = r.Value.Value
        let json = txt64 |> Convert.FromBase64String |> System.Text.UTF8Encoding.Default.GetString
        let sttngs = System.Text.Json.JsonSerializer.Deserialize<ServiceSettings>(json)
        sttngs


module Config =
    let CONFIG_PATH = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"../../../FsOpenAI.Server/wwwroot/" + C.APP_CONFIG_PATH)

    let saveConfig (config:AppConfig) (path:string) =
        let folder = Path.GetDirectoryName(path)
        if Directory.Exists folder |> not then Directory.CreateDirectory folder |> ignore
        let json = JsonSerializer.Serialize(config,options=Utils.serOptions())
        System.IO.File.WriteAllText(path,json)

    let saveSamples (samples:SamplePrompt list) (file:string) =
        let folder = Path.GetDirectoryName(file)
        if Directory.Exists folder |> not then Directory.CreateDirectory folder |> ignore
        let json = JsonSerializer.Serialize(samples,options=Utils.serOptions())
        System.IO.File.WriteAllText(file,json)
        printfn $"saved samples {Path.GetFullPath(file)}"

    let installClientFiles sourcePath =
        let dest = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"../../../FsOpenAI.Client/wwwroot")
        File.Copy(sourcePath @@ "appSettings.json", dest @@ "appSettings.json",true)
        let destImgsPath = dest @@ "app" @@ "imgs"
        Directory.GetFiles destImgsPath |> Seq.iter File.Delete
        Directory.GetFiles(sourcePath @@ "app" @@ "imgs")
        |> Seq.iter(fun f ->
            let dst = destImgsPath @@ (Path.GetFileName(f))
            File.Copy(f,dst,true)
            printfn $"Copied {dst}")

    let rec copyDir sourceDir destDir =
        if Directory.Exists sourceDir |> not then failwith $"{sourceDir} does  not exist"
        if Directory.Exists destDir |> not then Directory.CreateDirectory destDir |> ignore
        Directory.GetFiles(sourceDir) |> Seq.iter (fun f ->
            let destFile = destDir @@ Path.GetFileName(f)
            printfn $"Template: copied {destFile}"
            File.Copy(f,destFile,true))
        Directory.GetDirectories(sourceDir)
        |> Seq.iter(fun sdir -> copyDir sdir (destDir @@ Path.GetFileName(sdir)))

    let installTemplates sourcePath =
        let dest = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"../../../FsOpenAI.Server/wwwroot")
        let destTemplatesPath = dest @@ "app" @@ "Templates"
        if Directory.Exists destTemplatesPath then Directory.Delete(destTemplatesPath,true)
        copyDir sourcePath destTemplatesPath

module Indexes =
    open Azure.Search.Documents.Models
    open Azure.Search.Documents.Indexes.Models
    let WAIT_MULTIPLIER = 2

    let newField name = SemanticField(fieldName=name)

    type Doc = {File:string; Page:int; Chunk:string; Link:string}
    type DocEmb = {Time:DateTime; Doc:Doc; Embeddings:float32[]}

        //define the index format
    let indexDefinition(name) =
        let vectorSearchProfileName = "my-vector-profile"
        let vectorSearchHsnwConfig = "my-hsnw-vector-config"
        let idx = SearchIndex(name)
        idx.VectorSearch <- new VectorSearch()
        idx.VectorSearch.Algorithms.Add (new HnswAlgorithmConfiguration(vectorSearchHsnwConfig))
        idx.VectorSearch.Profiles.Add(new VectorSearchProfile(vectorSearchProfileName,vectorSearchHsnwConfig))
        let flds : SearchField list =
            [
                !> SimpleField("id", SearchFieldDataType.String, IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true)
                !> SearchableField("title", IsFilterable = true, IsSortable = true )
                !> SearchableField("sourcefile", IsFilterable = true, IsSortable = true )
                !> SearchableField("content", IsFilterable = true)
                !> VectorSearchField("contentVector", modelDimensions, vectorSearchProfileName)
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
        (recreate:bool)
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
                })
        async {
            let indexClient = indexClient()
            let searchClient = indexClient.GetSearchClient(idx.Name)
            if recreate then
                let! resp = indexClient.DeleteIndexAsync(idx) |> Async.AwaitTask       //delete index
                ()
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
                let embModel = "text-embedding-ada-002"
                let t = client.GetEmbeddingsAsync(EmbeddingsOptions(embModel,[doc.Chunk])) |> Async.AwaitTask
                let! emb = submitLoop "embedding" 0 t
                return {Time = t1; Doc=doc; Embeddings = emb.Value.Data.[0].Embedding.ToArray() }
            })

    let docTextPdf (r:Readers.IDocReader) =
        let pages = [for i in 0 .. r.GetPageCount()-1 -> i, r.GetPageReader(i).GetText()]
        pages
        |> Seq.collect(fun (i,t) -> TextChunker.SplitPlainTextParagraphs(ResizeArray [t],1500,100) |> Seq.map(fun c -> i,c))
        |> Seq.toList

    let docTextHtml (path:String)  =
        let html = File.ReadAllText path
        try
            let doc = HtmlDocument.Parse html
            let txt = doc.Body().InnerText()
            let txt = System.Text.RegularExpressions.Regex.Unescape(txt)
            let chunks = TextChunker.SplitPlainTextParagraphs(TextChunker.SplitPlainTextLines(txt,maxTokensPerLine=100),1500,100) |> Seq.toList
            chunks |> List.indexed
        with ex ->
            printfn $"ex {ex.Message} %A{html}"
            []

    let shredHtmlAsync (folder:string) =
        if Directory.Exists folder |> not then failwith $"folder {folder} does not exist"
        let root = folder
        let (@@) a b = Path.Combine(a,b)
        let docName (s:string) = Path.GetFileName(s)

        let docsFiles = Directory.GetFiles(root,"*.html") //get all htmls to shred
        let citations = root @@ "citations.csv"          //html link for each pdf document
                                                         //for hyperlinking from search results view
                                                         //format: Url, document file name without path
        if File.Exists citations |> not then failwith $"Citations file not found at {citations}"

        let citationsMap =
            CsvFile.Load(citations, hasHeaders=true).Rows
            |> Seq.map(fun r -> r.[1],r.[0])              //hyperlink url, document file name (without path)
            |> Map.ofSeq

        let readDocs() =
            docsFiles
            |> Seq.map(fun x -> docName x,x)
            |> Seq.map(fun (d,fn) -> d, docTextHtml fn)
            |> Seq.collect(fun (d,txs) -> txs |> Seq.map(fun (i,c) ->
                let baseLink = citationsMap.[d]
                let link = $"{baseLink}"
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


        let readDocs() =
            docsFiles
            |> Seq.map(fun x -> docName x,x)
            |> Seq.map(fun (d,fn) -> d, docTextPdf (DocLib.Instance.GetDocReader(fn,Models.PageDimensions(1.0))))
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

module MetaIndex =
    open Azure.Search.Documents.Indexes.Models

    //define the index format
    let metaIndex(name) =
        let newField = Indexes.newField
        let idx = SearchIndex(name)
        idx.SemanticSearch <- new SemanticSearch()
        idx.SemanticSearch.Configurations.Add(
            let p1 = SemanticPrioritizedFields(TitleField  = newField "title")
            p1.KeywordsFields.Add(newField "user")
            p1.KeywordsFields.Add(newField "description")
            p1.KeywordsFields.Add(newField "userIndexCreateTime")
            p1.KeywordsFields.Add(newField "userIndexFriendlyName")
            p1.KeywordsFields.Add(newField "groups")
            p1.KeywordsFields.Add(newField "isVirtual")
            p1.KeywordsFields.Add(newField "parents")
            let ssM = new SemanticConfiguration(
                        "meta-config",
                        prioritizedFields = p1)
            ssM)
        let flds : SearchField list =
            [
                !> SimpleField("id", SearchFieldDataType.String, IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true)
                !> SearchableField("title", IsFilterable = true, IsSortable = true )
                !> SearchableField("user", IsFilterable = true, IsSortable = true )
                !> SearchableField("userIndexCreateTime", IsFilterable = true, IsSortable = true )
                !> SearchableField("userIndexFriendlyName", IsFilterable = true, IsSortable = true )
                !> SearchableField("description", IsFilterable = true, IsSortable = true )
                !> SearchableField("groups", collection=true,IsFilterable=true)
                !> SearchableField("isVirtual", IsFilterable = true, IsSortable = true )
                !> SearchableField("parents", collection=true,IsFilterable=true)
            ]
        flds |> List.iter idx.Fields.Add
        idx

    let loadMeta indexName docs =
        let hasCycle = FsOpenAI.GenAI.Indexes.validateMeta docs
        if hasCycle then failwith "cycle detected"
        docs
        |> AsyncSeq.ofSeq
        |> AsyncSeq.map(FsOpenAI.GenAI.Indexes.toDoc)
        |> (Indexes.loadIndexAsync true (metaIndex indexName))
        |> Async.map(fun  x -> printfn "done"; x)
        |> Async.Start

module ModelDefs =
    let embedding =
        [
            {Backend=AzureOpenAI; Model="text-embedding-ada-002"; TokenLimit=8192} //only azure is supported for embeddings
        ]
    let shortChat =
        [
            {Backend=AzureOpenAI; Model="gpt-4o"; TokenLimit=8000};
            {Backend=OpenAI; Model="gpt-4o"; TokenLimit=127000};
        ]
    let longChat =
        [
            {Backend=AzureOpenAI; Model="gpt-4o"; TokenLimit=30000}
            {Backend=OpenAI; Model="gpt-4o"; TokenLimit=127000}
        ]
    let completion =
        [
            {Backend=AzureOpenAI; Model="text-davinci-003"; TokenLimit=4000}
            {Backend=OpenAI; Model="gpt-4o"; TokenLimit=4000}
        ]
    let lowcost =
        [
            {Backend=AzureOpenAI; Model="gpt-35-turbo"; TokenLimit=8000}
            {Backend=OpenAI; Model="gpt-4o"; TokenLimit=8000}
        ]
    let modelsConfig =
        {
            EmbeddingsModels = embedding
            ShortChatModels = shortChat
            LongChatModels = longChat
            CompletionModels = completion
            LowCostModels = lowcost
        }
