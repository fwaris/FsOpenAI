module SemanticVectorSearch
open System
open Microsoft.SemanticKernel.Memory
open System.Runtime.CompilerServices
open Azure.AI.OpenAI
open Azure.Search.Documents.Models
open Azure.Search.Documents
open FSharp.Control

type CognitiveSearch(srchClient:SearchClient,embeddingClient:OpenAIClient,embeddingModel:string,vectorField,contentField,sourceRefField,descriptionField) =
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
        //let emb = r.Document.[vectorField] :?> float32 []
        MemoryQueryResult(toMetadata(r.Document),r.RerankerScore ?> 1.0 ,Nullable())
        
    interface ISemanticTextMemory with
        member this.GetAsync(collection, key, withEmbedding, cancellationToken) = raise (System.NotImplementedException())
        member this.GetCollectionsAsync(cancellationToken) = raise (System.NotImplementedException())
        member this.RemoveAsync(collection, key, cancellationToken) = raise (System.NotImplementedException())
        member this.SaveInformationAsync(collection, text, id, description, additionalMetadata, cancellationToken) = raise (System.NotImplementedException())
        member this.SaveReferenceAsync(collection, text, externalId, externalSourceName, description, additionalMetadata, cancellationToken) = raise (System.NotImplementedException())
        member this.SearchAsync(collection, query, limit, minRelevanceScore, withEmbeddings, [<EnumeratorCancellation>] cancellationToken) = 
            asyncSeq {
                let! resp = embeddingClient.GetEmbeddingsAsync(embeddingModel,EmbeddingsOptions(query)) |> Async.AwaitTask
                let eVec = resp.Value.Data.[0].Embedding
                let vec = SearchQueryVector(
                    KNearestNeighborsCount = limit,
                    Fields = vectorField,
                    Value = eVec)
                let so = SearchOptions(Vector=vec, Size=limit)
                [idField; contentField; sourceRefField; descriptionField] |> Seq.iter so.Select.Add 
                let! srchRslt = srchClient.SearchAsync<SearchDocument>(null,so)  |> Async.AwaitTask
                let rs = srchRslt.Value.GetResultsAsync() |> AsyncSeq.ofAsyncEnum |> AsyncSeq.map toMemoryResult
                yield! rs                
            }
            |> AsyncSeq.toAsyncEnum
   
                
        
            
            
