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

let indexName = FsOpenAI.Client.C.META_INDEX

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
        ]
    flds |> List.iter idx.Fields.Add
    idx

let loadMeta docs =
    docs
    |> AsyncSeq.ofSeq
    |> AsyncSeq.map(Indexes.toDoc)
    |> (Index.loadIndexAsync true (metaIndex indexName))
    |> Async.map(fun  x -> printfn "done"; x)
    |> Async.Start

module AccountingIndexesProd =
    let docDesc = 
        [
            //"att-sec", "SEC: AT&T filings"
            //"tmobile-sec","SEC: T-Mobile filings"
            //"verizon-sec","SEC: Verizon filings"
            "accounting-policy","Accounting: All policy docs"
            "big-4-guides","Accounting: Big 4 guides"
            "dt-policies","Accounting: DT Policies"
            "tmus-memos","TMUS: Memos"
            "tmus-policies","TMUS: Policies"
        ]
    let docs = docDesc |> List.map(fun (n,d) -> {MetaIndexEntry.Default with title=n; description=d; groups=["accounting"]})
    
    let load() = loadMeta docs 

module AccountingIndexesPoc =
    let docDesc = 
        [
            "att-sec", "SEC: AT&T filings"
            "tmobile-sec","SEC: T-Mobile filings"
            "verizon-sec","SEC: Verizon filings"
            "accounting-policy","Accounting: All policy docs"
            "gaap","FASAB guidelines for US goverment bodies"
        ]
    let docs = docDesc |> List.map(fun (n,d) -> {MetaIndexEntry.Default with title=n; description=d; groups=["accounting"]})
    
    let load() = loadMeta docs 

module GcIndexesProd =
    let docDesc = 
        [
            "ericsson-gc-academy", "GC Academy: Ericsson Construction and Integration"
            "nokia-gc-academy","GC Academy: Nokia Construction and Integration"
            "ixr-e-gc-academy","GC Academy: IXR-e"
            "t-mobile-standards", "Construction: T-Mobile Standards"
            "ericsson-install", "Installation: Ericsson Guides"
            "nokia-install", "Installation: Nokia Guides"
        ]
    let docs = docDesc |> List.map(fun (n,d) -> {MetaIndexEntry.Default with title=n; description=d; groups=["gc"]})
    
    let load() = loadMeta docs 

(*

Env.installSettings ("%USERPROFILE%/.fsopenai/prod/ServiceSettings.json")
AccountingIndexesProd.load()

Env.installSettings ("%USERPROFILE%/.fsopenai/poc/ServiceSettings.json")
AccountingIndexesPoc.load()


//test
let ys = Indexes.metaIndexEntries (indexClient()) ["accounting"] |> Async.RunSynchronously


//gc
Env.installSettings "%USERPROFILE%/.fsopenai/gc/ServiceSettings.json"
GcIndexesProd.load()
*)

