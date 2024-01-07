#load "Env.fsx"
(*
Create the meta index that lists all the indexes visibile to the application
*)

open System.IO
open Azure
open Azure.Search.Documents
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Models
open Azure.Search.Documents.Indexes.Models
open FSharp.Control
open FsOpenAI.Client
open Env

//define the index format
let metaIndex(name) =     
    let newField = Index.newField
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
    let hasCycle = Indexes.validateMeta docs
    if hasCycle then failwith "cycle detected"
    docs
    |> AsyncSeq.ofSeq
    |> AsyncSeq.map(Indexes.toDoc)
    |> (Index.loadIndexAsync true (metaIndex indexName))
    |> Async.map(fun  x -> printfn "done"; x)
    |> Async.Start


module AccountingIndexesPoc_Tree =
    let docDesc = 
        [
            "sec-all", "SEC: All filings",true,[]
            "att-sec", "SEC: AT&T filings",false,["sec-all"]
            "tmobile-sec","SEC: T-Mobile filings",false,["sec-all"]
            "verizon-sec","SEC: Verizon filings",false,["sec-all"]
            "accounting-policy","Accounting: All policy docs",false,[]
            "gaap","FASAB guidelines for US goverment bodies",false,["accounting-policy"]
        ]
    let docs = 
        docDesc 
        |> List.map(fun (n,d,isV,parents) -> 
            {MetaIndexEntry.Default with 
                title=n
                description=d
                groups=["accounting"]
                isVirtual=isV;
                parents=parents}
            )
    
    let load() = loadMeta C.DEFAULT_META_INDEX docs 


(*

Env.installSettings ("%USERPROFILE%/.fsopenai/poc/ServiceSettings.json")
AccountingIndexesPoc_Tree.load()


//test
let ys = Indexes.metaIndexEntries (indexClient()) ["accounting"] C.DEFAULT_META_INDEX |> Async.RunSynchronously

*)

