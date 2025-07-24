module SemanticVectorSearch
open System
open Microsoft.SemanticKernel.Memory
open System.Runtime.CompilerServices
open Azure.Search.Documents.Models
open Azure.Search.Documents
open FSharp.Control
open Microsoft.SemanticKernel.Embeddings

type SearchMode = Semantic | Hybrid | Plain

type CognitiveSearch
    (
        mode,
        srchClient:SearchClient,
        embeddingClient:ITextEmbeddingGenerationService,
        vectorFields,
        contentField,
        sourceRefField,
        descriptionField) =

    let idField = "id"

    let toMetadata (d:SearchDocument) =
        let id = d.[idField] :?> string
        let content = d.[contentField] :?> string     
        let source = d.[sourceRefField] :?> string
        let desc = d.[descriptionField] :?> string
        MemoryRecordMetadata(
            isReference = true,
            id = id,
            text = content,
            description = desc,
            externalSourceName = source,
            additionalMetadata = srchClient.IndexName)

    let ( ?> ) (a:Nullable<float>) def = if a.HasValue then a.Value else def

    let toMemoryResult(r:SearchResult<SearchDocument>) =
        let vectorField = Seq.head vectorFields
        let embRaw : obj = if r.Document.ContainsKey vectorField then r.Document.[vectorField] else null
        let emb : Nullable<ReadOnlyMemory<float32>> =
            if embRaw = null then Nullable()
            else
                let emb : float32[] = embRaw :?> obj[] |> Array.map (fun x -> x :?> float |> float32)
                emb |> ReadOnlyMemory |> Nullable
        MemoryQueryResult(toMetadata(r.Document),r.Score ?> 1.0 ,emb)
        
    interface ISemanticTextMemory with
        member this.GetAsync(collection, key, withEmbedding, kernel, cancellationToken) = raise (System.NotImplementedException())
        member this.GetCollectionsAsync(kernel, cancellationToken) = raise (System.NotImplementedException())
        member this.RemoveAsync(collection, key, kernel, cancellationToken) = raise (System.NotImplementedException())
        member this.SaveInformationAsync(collection, text, id, description, additionalMetadata, kernel, cancellationToken) = raise (System.NotImplementedException())
        member this.SaveReferenceAsync(collection, text, externalId, externalSourceName, description, additionalMetadata, kernel, cancellationToken) = raise (System.NotImplementedException())
        member this.SearchAsync(collection, query, limit, minRelevanceScore, withEmbeddings, kernel, [<EnumeratorCancellation>] cancellationToken) = 
            asyncSeq {
                let so = SearchOptions(Size=limit)
                [idField; contentField; sourceRefField; descriptionField] |> Seq.iter so.Select.Add 
                match mode with 
                | Semantic | Hybrid -> 
                    let! resp = embeddingClient.GenerateEmbeddingsAsync(ResizeArray[query]) |> Async.AwaitTask
                    let eVec = resp.[0]
                    let vec = VectorizedQuery(KNearestNeighborsCount = limit,vector = eVec)
                    vectorFields |> Seq.iter vec.Fields.Add
                    //so.Filter <- query
                    so.VectorSearch <- new VectorSearchOptions()
                    so.VectorSearch.Queries.Add(vec)
                    let qSearch = if mode = SearchMode.Hybrid then query else null 
                    let! srchRslt = srchClient.SearchAsync<SearchDocument>(qSearch,so)  |> Async.AwaitTask
                    let rs = srchRslt.Value.GetResultsAsync() |> AsyncSeq.ofAsyncEnum |> AsyncSeq.map toMemoryResult                
                    yield! rs                
                | Plain -> 
                    vectorFields |> Seq.iter so.Select.Add
                    let! srchRslt = srchClient.SearchAsync<SearchDocument>(query,so)  |> Async.AwaitTask
                    let rs = srchRslt.Value.GetResultsAsync() |> AsyncSeq.ofAsyncEnum |> AsyncSeq.map toMemoryResult                
                    yield! rs
            }
            |> AsyncSeq.toAsyncEnum                
