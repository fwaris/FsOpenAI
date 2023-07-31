namespace FsOpenAI.Client
open System
open FSharp.Control
open Azure.AI.OpenAI
open Azure
open Azure.Search.Documents
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Models
open Azure.Search.Documents.Indexes.Models

module Indexes =
    let refFieldSet = set ["id"; "content"; "contentVector"; "sourcefile"; "category"; "title"]

    let searchClient (parms:ServiceSettings) = 
        match parms.AZURE_SEARCH_ENDPOINTS with 
        | [] -> failwith "No Azure Cognitive Search endpoints configured"
        | xs ->
            let ep = Utils.randSelect xs
            SearchIndexClient(Uri ep.ENDPOINT,AzureKeyCredential(ep.API_KEY))

    let filterIndex (idx:SearchIndex) =
        let idSet = idx.Fields |> Seq.map(fun c -> c.Name) |> set
        let diff = Set.intersect idSet refFieldSet
        if diff.Count >= refFieldSet.Count then
            let vecField = idx.Fields |> Seq.find (fun x -> x.Name = "contentVector")
            vecField.Type.IsCollection
        else 
            false            

    let fetch (parms:ServiceSettings) =
        async{
            try
                let searchClient = searchClient parms
                printfn "fetching indexes"
                let pages = searchClient.GetIndexesAsync().AsPages() 
                let! pages = pages |> AsyncSeq.ofAsyncEnum |> AsyncSeq.toListAsync
                printfn "got indexes"
                let idxs = pages |> Seq.collect(fun page -> page.Values) |> Seq.toList
                let idxs = idxs |> List.filter filterIndex                    
                if idxs.IsEmpty then 
                    return [],Some($"No indexes found containing the expected field types '{refFieldSet |> Seq.toList}'")
                else
                    let idxs = idxs |> List.map(fun x -> Azure {|Name=x.Name|})
                    return idxs,None
            with ex -> 
                printfn "Error fetching index data %s" ex.Message
                return [],Some(ex.Message)                
        }    
    