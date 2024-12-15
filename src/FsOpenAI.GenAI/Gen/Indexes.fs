namespace FsOpenAI.GenAI
open System
open FSharp.Control
open Azure
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Models
open Azure.Search.Documents.Indexes.Models
open FsOpenAI.Shared
open FsOpenAI.GenAI.Endpoints

type MetaIndexEntry =
    {

        title : string
        tag: string
        description: string
        groups : string list        //tells under which templates this index is visible
        parents : string list       //tells which indexes this index is a child of
        isVirtual : bool            //if true is not a real index but used to group other indexes
        user : string
        userIndexCreateTime : string
        userIndexFriendlyName : string
    }
    with
        static member Default =
            {
                tag = ""
                title = ""
                description = ""
                groups = []
                parents = []
                isVirtual = false
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
            let ep = Endpoints.randSelect xs
            SearchIndexClient(Uri ep.ENDPOINT,AzureKeyCredential(ep.API_KEY))

    let getParents (p:SearchDocument) =
        if p.Keys.Contains("parents") then
            p.["parents"] :?> obj[] |> Seq.map string |> Seq.toList
        else
            []
    let isVirtual (p:SearchDocument) =
        if p.Keys.Contains("isVirtual") then
           string  p.["isVirtual"] |> Boolean.Parse
        else
            false

    let toMeta (p:SearchDocument) =
        //let tagValue = if string p.["tag"] = null then string p.["title"] else string p.["tag"]
        let tagexists =  p.ContainsKey("tag")
        let tag = if not tagexists then string p.["title"] else string p.["tag"]
        {
            tag = tag
            title = string p.["title"]
            description = string p.["description"]
            user = string p.["user"]
            groups = p.["groups"] :?> obj[] |> Seq.map string |> Seq.toList
            parents = getParents p
            isVirtual = isVirtual p
            userIndexCreateTime = string p.["userIndexCreateTime"]
            userIndexFriendlyName = string p.["userIndexFriendlyName"]
        }

    let toDoc (m:MetaIndexEntry) =
        let p = SearchDocument()
        p.["id"] <- Guid.NewGuid().ToString()
        p.["tag"] <- m.tag
        p.["title"] <- m.title
        p.["description"] <- m.description
        p.["groups"] <- m.groups
        p.["user"] <- m.user
        p.["parents"] <- m.parents
        p.["isVirtual"] <- string m.isVirtual
        p.["userIndexCreateTime"] <- m.userIndexCreateTime
        p.["userIndexFriendlyName"] <- m.userIndexFriendlyName
        p

    let matchTemplate ts idx =
        if Set.isEmpty ts then true                                                     //apps with empty IndexGroup matches any
        elif idx.groups.IsEmpty then true                                            //indexes with empty templates are not filtered out
        else idx.groups |> List.exists (fun y -> ts |> Set.contains (y.ToLower()))

    let buildIdxTree (idxs:MetaIndexEntry list) =
        let rec build acc rs =
            match rs with
            | [] -> acc
            | x::rest ->
                let acc =
                    (acc, x.parents)
                        ||> List.fold (fun acc y ->
                            acc
                            |> Map.tryFind y
                            |> Option.map (fun ps -> acc |> Map.add y (x::ps))
                            |> Option.defaultValue (acc |> Map.add y [x]))
                build acc rest
        let pMap = build Map.empty idxs
        let nMap = idxs  |> List.map(fun x->x.tag,x) |> Map.ofList
        let rec buildNode p =
            let r = nMap.[p]
            let chs = pMap |> Map.tryFind p |> Option.defaultValue []
            let idx = if r.isVirtual then Virtual p else Azure p
            {
                Idx=idx
                Description=r.description
                IndexName = if r.isVirtual then None else Some r.title
                Tag =r.tag
                Children=chs
                    |> List.map (fun x -> x.tag)
                    |> List.map buildNode
                    |> List.sortBy (fun x -> x.Idx)}
        let roots = idxs |> List.filter(fun x -> x.parents.IsEmpty)
        roots |> List.map(fun x -> buildNode x.tag)

    let metaIndexEntries (svcClient:SearchIndexClient)  (templates:string list) metaIndex =
        async {
            let ts = templates |> List.map (fun x->x.ToLower()) |> set
            printfn "looking for meta index"
            let! indexClient = svcClient.GetIndexAsync(metaIndex) |> Async.AwaitTask
            if indexClient.HasValue then
                printfn "found meta index"
                let idxClient = svcClient.GetSearchClient(metaIndex)
                let! rs = idxClient.SearchAsync<SearchDocument>("") |> Async.AwaitTask
                let midxs =
                    rs.Value.GetResultsAsync().AsPages()
                    |> AsyncSeq.ofAsyncEnum
                    |> AsyncSeq.collect (fun x -> AsyncSeq.ofSeq x.Values )
                    |> AsyncSeq.toBlockingSeq
                    |> Seq.map (fun r -> toMeta r.Document)
                    |> Seq.toList
                return Some midxs
            else
                printfn "meta index not found"
                return None
        }

    ///detect possible cycles
    let validateMeta (metaIndexEntries:MetaIndexEntry list) =
        let rels = metaIndexEntries |> List.map(fun x -> x.title, set x.parents) |> Map.ofList
        let rec detectCycle parent x =
            let parents = rels |> Map.tryFind x |> Option.defaultValue Set.empty
            if parents.Contains parent then
                true
            else
                parents |> Set.exists (detectCycle parent)
        metaIndexEntries |> List.map _.title |> List.exists(fun x -> detectCycle x x)


    //looks for indexes that contain the expected fields
    let filterIndex (idx:SearchIndex) =
        let idSet = idx.Fields |> Seq.map(fun c -> c.Name) |> set
        let diff = Set.intersect idSet refFieldSet
        if diff.Count >= refFieldSet.Count then
            let vecField = idx.Fields |> Seq.find (fun x -> x.Name = "contentVector")
            vecField.Type.IsCollection
        else
            false

    //if no meta-index is found, find indexes that contain the expected fields
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
                let idxs = idxs |> List.map(fun x -> {Idx=Azure x.Name; IndexName=Some ""; Description=""; Tag="";Children=[]})
                return idxs,None
        }

    let indexInfo (xs:MetaIndexEntry list) =
        async{
            let tree = buildIdxTree xs
            return tree,None
        }

    let defaultIndexInfo (svcClient:SearchIndexClient) =
        async {
            let! idxs,err = findCompatibleIndexes svcClient
            return idxs,err
        }

    let fetch (parms:ServiceSettings)  templates metaIndex =
        async{
            try
                let svcClient = searchServiceClient parms
                let! metaIdx = metaIndexEntries svcClient templates metaIndex
                let! tree,err =
                    match metaIdx with
                    | Some xs -> indexInfo xs
                    | None -> defaultIndexInfo svcClient
                return tree,err
            with ex ->
                printfn "Error fetching index data %s" ex.Message
                return [],Some(ex.Message)
        }
