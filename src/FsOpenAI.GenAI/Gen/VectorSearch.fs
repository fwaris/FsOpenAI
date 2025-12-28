module FsOpenAI.GenAI.VectorSearch
open System
open Microsoft.Extensions.VectorData

type IndexedDocument = {
    [<VectorStoreKey>]
    id : string
    
    [<VectorStoreVector(Dimensions=1536, DistanceFunction=DistanceFunction.CosineDistance)>]
    contentVector : ReadOnlyMemory<float>
    
    [<VectorStoreData>]
    content : string

    [<VectorStoreData>]
    sourcefile : string

    [<VectorStoreData>]
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

module VectorSearch =
    let search query = async {
        let store = InMemoryVectorStore()
    }