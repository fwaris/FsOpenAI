namespace FsOpenAI.Client
open System
open FSharp.Control

module Utils =
    let rng = Random()
    let randSelect (ls:_ list) = ls.[rng.Next(ls.Length)]

    let mutable private id = 0
    let nextId() = Threading.Interlocked.Increment(&id)

    let notEmpty (s:string) = String.IsNullOrWhiteSpace s |> not
    let isEmpty (s:string) = String.IsNullOrWhiteSpace s 

    let isOpen key map = map |> Map.tryFind key |> Option.defaultValue false

    let asAsyncSeq<'t> (xs:System.Collections.Generic.IAsyncEnumerable<'t>) = 
        asyncSeq {
            let mutable hs = false
            let xs = xs.GetAsyncEnumerator()
            let! hasNext = task{return! xs.MoveNextAsync()} |> Async.AwaitTask
            hs <- hasNext
            while hs do
                yield xs.Current
                let! hasNext = task{return! xs.MoveNextAsync()} |> Async.AwaitTask
                hs <- hasNext
            xs.DisposeAsync() |> ignore
        }

    exception NoOpenAIKey

module C =
    let LS_OPENAI_KEY = "LS_OPENAI_KEY"
    let MAIN_SETTINGS = "MAIN_SETTINGS"