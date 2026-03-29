module FsOpenAI.GenAI.VectorSearch

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Azure.Search.Documents
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Models
open FSharp.Control
open Microsoft.Extensions.AI
open Microsoft.Extensions.VectorData
open Microsoft.SemanticKernel.Connectors.AzureAISearch
open FsOpenAI.Shared

type IndexedDocument = {
    [<VectorStoreKey>]
    id : string
    
    [<VectorStoreVector(Dimensions=1536, DistanceFunction=DistanceFunction.CosineDistance)>]
    contentVector : ReadOnlyMemory<float32>
    
    [<VectorStoreData(IsFullTextIndexed = true)>]
    content : string

    [<VectorStoreData>]
    sourcefile : string

    [<VectorStoreData(IsFullTextIndexed = true)>]
    title : string
}

type MetaDocument = {
    [<VectorStoreKey>]
    id : string

    [<VectorStoreData>]
    title : string
    
    [<VectorStoreData>]
    tag : string
    
    [<VectorStoreData>]
    description : string
    
    [<VectorStoreData>]
    isVirtual : bool
    
    [<VectorStoreData>]
    parents : string list
}

type SearchMode =
    | Semantic
    | Hybrid
    | Plain

type SearchIndex =
    {
        Name : string
        Collection : AzureAISearchCollection<string, IndexedDocument>
        SearchClient : SearchClient
    }

let createSearchIndex (indexClient:SearchIndexClient) name =
    {
        Name = name
        Collection = AzureAISearchCollection<string, IndexedDocument>(indexClient, name)
        SearchClient = indexClient.GetSearchClient(name)
    }

let createSearchIndexes indexClient names =
    names |> List.map (createSearchIndex indexClient)

let private scoreOrDefault (score: Nullable<double>) =
    if score.HasValue then float score.Value else 0.0

let private ensureText (value:string) = if isNull value then String.Empty else value

let private toDocRef score (doc:IndexedDocument) =
    {
        Text = ensureText doc.content
        Embedding = [||]
        Ref = ensureText doc.sourcefile
        Title = ensureText doc.title
        Id = String.Empty
        Relevance = score
        SortOrder = None
    }

let private enumerateAsync (source:System.Collections.Generic.IAsyncEnumerable<'a>) =
    let enumerator = source.GetAsyncEnumerator(CancellationToken.None)
    let rec loop acc =
        async {
            let! hasNext = enumerator.MoveNextAsync().AsTask() |> Async.AwaitTask
            if hasNext then
                return! loop (enumerator.Current :: acc)
            else
                return List.rev acc
        }
    async {
        try
            return! loop []
        finally
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult()
    }

let private fromVectorResults (results:VectorSearchResult<IndexedDocument> list) =
    results
    |> List.choose (fun r ->
        let record = r.Record
        if obj.ReferenceEquals(record, null) then None else Some (toDocRef (scoreOrDefault r.Score) record))

let private fromSearchResults (results:SearchResult<IndexedDocument> list) =
    results
    |> List.choose (fun r ->
        let document = r.Document
        if obj.ReferenceEquals(document, null) then None else Some (toDocRef (scoreOrDefault r.Score) document))

let private semanticSearch vector maxDocs (index:SearchIndex) =
    async {
        let options = VectorSearchOptions<IndexedDocument>()
        options.IncludeVectors <- false
        let! raw =
            index.Collection.SearchAsync(vector, maxDocs, options)
            |> fun r -> (r :> System.Collections.Generic.IAsyncEnumerable<_>)
            |> enumerateAsync
        return fromVectorResults raw
    }

let private hybridSearch vector query maxDocs (index:SearchIndex) =
    async {
        let options = HybridSearchOptions<IndexedDocument>()
        options.IncludeVectors <- false
        let keywords =
            if String.IsNullOrWhiteSpace query then
                ResizeArray [ "" ]
            else
                ResizeArray [ query ]
        let! raw =
            index.Collection.HybridSearchAsync(vector, keywords, maxDocs, options)
            |> fun r -> (r :> System.Collections.Generic.IAsyncEnumerable<_>)
            |> enumerateAsync
        return fromVectorResults raw
    }

let private keywordSearch query maxDocs (index:SearchIndex) =
    async {
        let selectFields = [| "id"; "content"; "sourcefile"; "title" |]
        let searchOptions = SearchOptions(Size = Nullable maxDocs)
        selectFields |> Array.iter searchOptions.Select.Add
        let! response = index.SearchClient.SearchAsync<IndexedDocument>(query, searchOptions) |> Async.AwaitTask
        let! raw =
            response.Value.GetResultsAsync()
            |> fun r -> (r :> System.Collections.Generic.IAsyncEnumerable<SearchResult<IndexedDocument>>)
            |> enumerateAsync
        return fromSearchResults raw
    }

let private assignIds (docs:DocRef list) =
    docs |> List.mapi (fun idx doc -> { doc with Id = string (idx + 1) })

let search (embeddingClient:IEmbeddingGenerator<string, Embedding<float32>>) mode maxDocs query indexes : Async<DocRef list> =
    async {
        if maxDocs <= 0 || List.isEmpty indexes then
            return List.empty<DocRef>
        else
            let! vectorOpt =
                match mode with
                | Plain -> async.Return None
                | _ ->
                    async {
                        let! resp = embeddingClient.GenerateAsync([query]) |> Async.AwaitTask
                        return resp.[0].Vector |> Some
                    }

            let searches =
                indexes
                |> List.map (fun idx ->
                    match mode, vectorOpt with
                    | Plain, _ -> keywordSearch query maxDocs idx
                    | Semantic, Some vector -> semanticSearch vector maxDocs idx
                    | Hybrid, Some vector -> hybridSearch vector query maxDocs idx
                    | _, None -> async { return List.empty<DocRef> })

            let! results = searches |> Async.Parallel
            return
                results
                |> Array.toList
                |> List.collect id
                |> List.sortByDescending (fun doc -> doc.Relevance)
                |> assignIds
                |> List.truncate maxDocs
    }
