namespace FsOpenAI.Client
open System
open FSharp.Control
open Azure.AI.OpenAI
open Azure
open Azure.Search.Documents
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Models
open Azure.Search.Documents.Indexes.Models

type MetaIndexEntry = 
    {
        title : string
        description: string
        groups : string list
        user : string
        userIndexCreateTime : string
        userIndexFriendlyName : string
    }
    with 
        static member Default = 
            {
                title = ""
                description = ""
                groups = []
                user = ""
                userIndexCreateTime = ""
                userIndexFriendlyName = ""
            }

module Indexes =
    let refFieldSet = set ["id"; "content"; "contentVector"; "sourcefile"; "category"; "title"]

    let searchServiceClient (parms:ServiceSettings) = 
        match parms.AZURE_SEARCH_ENDPOINTS with 
        | [] -> failwith "No Azure Cognitive Search endpoints configured"
        | xs ->
            let ep = GenUtils.randSelect xs
            SearchIndexClient(Uri ep.ENDPOINT,AzureKeyCredential(ep.API_KEY))

    let toMeta (p:SearchDocument) =
        {
            title = string p.["title"]
            description = string p.["description"]
            user = string p.["user"]
            groups = p.["groups"] :?> obj[] |> Seq.map string |> Seq.toList
            userIndexCreateTime = string p.["userIndexCreateTime"]
            userIndexFriendlyName = string p.["userIndexFriendlyName"]
        }

    let toDoc (m:MetaIndexEntry) =
        let p = SearchDocument()
        p.["id"] <- Guid.NewGuid().ToString()
        p.["title"] <- m.title
        p.["description"] <- m.description
        p.["groups"] <- m.groups
        p.["user"] <- m.user
        p.["userIndexCreateTime"] <- m.userIndexCreateTime
        p.["userIndexFriendlyName"] <- m.userIndexFriendlyName
        p

    let matchTemplate ts idx = 
        if Set.isEmpty ts then true                                                     //apps with empty IndexGroup matches any
        elif idx.groups.IsEmpty then true                                            //indexes with empty templates are not filtered out
        else idx.groups |> List.exists (fun y -> ts |> Set.contains (y.ToLower()))
      
    let metaIndexEntries (svcClient:SearchIndexClient) (templates:string list) =        
        async {
            let ts = templates |> List.map (fun x->x.ToLower()) |> set
            printfn "looking for meta index"
            let! indexClient = svcClient.GetIndexAsync(C.META_INDEX) |> Async.AwaitTask
            if indexClient.HasValue then 
                printfn "found meta index"
                let idxClient = svcClient.GetSearchClient(C.META_INDEX)
                let! rs = idxClient.SearchAsync<SearchDocument>("") |> Async.AwaitTask
                let midxs = 
                    rs.Value.GetResultsAsync().AsPages() 
                    |> AsyncSeq.ofAsyncEnum 
                    |> AsyncSeq.collect (fun x -> AsyncSeq.ofSeq x.Values )
                    |> AsyncSeq.toBlockingSeq 
                    |> Seq.map (fun r -> toMeta r.Document)
                    |> Seq.filter (matchTemplate ts)
                    |> Seq.map(fun m -> Azure {Name=m.title; Description=m.description})
                    |> Seq.toList
                return Some midxs
            else
                printfn "meta index not found"
                return None
        }

    let filterIndex (idx:SearchIndex) =
        let idSet = idx.Fields |> Seq.map(fun c -> c.Name) |> set
        let diff = Set.intersect idSet refFieldSet
        if diff.Count >= refFieldSet.Count then
            let vecField = idx.Fields |> Seq.find (fun x -> x.Name = "contentVector")
            vecField.Type.IsCollection
        else 
            false            

    let findCompatibleIndexes (svcClient:SearchIndexClient) =
        async{
            printfn "finding indexes"
            let pages = svcClient.GetIndexesAsync().AsPages() 
            let! pages = pages |> AsyncSeq.ofAsyncEnum |> AsyncSeq.toListAsync
            printfn "got indexes"
            let idxs = pages |> Seq.collect(fun page -> page.Values) |> Seq.toList
            let idxs = idxs |> List.filter filterIndex                    
            if idxs.IsEmpty then 
                return [],Some($"No indexes found containing the expected field types '{refFieldSet |> Seq.toList}'")
            else
                let idxs = idxs |> List.map(fun x -> Azure {Name=x.Name; Description=""})
                return idxs,None
        }


    let fetch (parms:ServiceSettings) templates =
        async{
            try
                let svcClient = searchServiceClient parms
                let! metaIdx = metaIndexEntries svcClient templates
                match metaIdx with 
                | Some xs -> return xs,None
                | None -> return! findCompatibleIndexes svcClient
            with ex -> 
                printfn "Error fetching index data %s" ex.Message
                return [],Some(ex.Message)                
        }    
    