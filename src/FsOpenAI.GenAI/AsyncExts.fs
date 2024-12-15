module AsyncExts
open System
open FSharp.Control
open System.Threading
open System.Threading.Tasks

module Async =
    let map f a = async.Bind(a, f >> async.Return)

module AsyncSeq =
    let mapAsyncParallelThrottled (parallelism:int) (f:'a -> Async<'b>) (s:AsyncSeq<'a>) : AsyncSeq<'b> = asyncSeq {
        use mb = MailboxProcessor.Start (ignore >> async.Return)
        use sm = new SemaphoreSlim(parallelism)
        let! err =
            s
            |> AsyncSeq.iterAsync (fun a -> async {
            let! _ = sm.WaitAsync () |> Async.AwaitTask
            let! b = Async.StartChild (async {
                try return! f a
                finally sm.Release () |> ignore })
            mb.Post (Some b) })
            |> Async.map (fun _ -> mb.Post None)
            |> Async.StartChildAsTask
        yield!
            AsyncSeq.unfoldAsync (fun (t:Task) -> async{
            if t.IsFaulted then
                return None
            else
                let! d = mb.Receive()
                match d with
                | Some c ->
                    let! d' = c
                    return Some (d',t)
                | None -> return None
            })
            err
    }
(*
      //implementation possible within AsyncSeq, with the supporting code available there
      let mapAsyncParallelThrottled (parallelism:int) (f:'a -> Async<'b>) (s:AsyncSeq<'a>) : AsyncSeq<'b> = asyncSeq {
        use mb = MailboxProcessor.Start (ignore >> async.Return)
        use sm = new SemaphoreSlim(parallelism)
        let! err =
          s
          |> iterAsync (fun a -> async {
            do! sm.WaitAsync () |> Async.awaitTaskUnitCancellationAsError
            let! b = Async.StartChild (async {
              try return! f a
              finally sm.Release () |> ignore })
            mb.Post (Some b) })
          |> Async.map (fun _ -> mb.Post None)
          |> Async.StartChildAsTask
        yield!
          replicateUntilNoneAsync (Task.chooseTask (err |> Task.taskFault) (async.Delay mb.Receive))
          |> mapAsync id }
*)

    let private _mapAsyncParallelUnits (units:string) (unitsPerMinute:float) (f:'a -> Async<'b>) (s:AsyncSeq<uint64*'a>) : AsyncSeq<'b> = asyncSeq {
        use mb = MailboxProcessor.Start (ignore >> async.Return)
        let resolution = 0.5 //minutes
        let mutable markTime = DateTime.Now.Ticks
        let mutable unitCount = 0uL

        let incrUnits i = Interlocked.Add(&unitCount,i)

        let reset i t =
            Interlocked.Exchange(&unitCount,i) |> ignore
            Interlocked.Exchange(&markTime,t) |> ignore

        //some randomness to stagger calls
        let rand5Pct() =
            let rng = Random()
            (rng.Next(0,5) |> float) / 100.0

        let! err =
            s
            |> AsyncSeq.iterAsync (fun (t,a) -> async {
                let unitCountF = incrUnits t |> float
                let elapsed = (DateTime.Now - DateTime(markTime)).TotalMinutes
                if elapsed > resolution then
                    //use a sliding rate
                    let t = DateTime.Now.AddMinutes (-resolution / 2.0)
                    let i = (uint64 (unitCountF / 2.0))
                    reset i t.Ticks
                let rate =
                    if elapsed < 0.001 then
                        let overagePct = rand5Pct()
                        unitsPerMinute * (1.0 + overagePct) //initially (elapsed ~= 0) assume small random overage so initial calls are staggered
                    else
                        unitCountF / elapsed |> min (unitsPerMinute * 2.0)  //cap rate to 2x
                printfn $"{units}/min: %0.0f{rate} [per sec %0.1f{rate/60.0}]"
                let overage = rate - unitsPerMinute
                if overage > 0.0 then
                    //how much of next resolution period we should wait?
                    //scale based on overage as %age of base rate
                    let overagePct = overage / unitsPerMinute + (rand5Pct())
                    let wait = resolution * overagePct * 60000.0 |> int
                    printfn $"wait sec %0.1f{float wait/1000.0}"
                    do! Async.Sleep wait
                let! b = Async.StartChild (async {
                    try return! f a
                    finally () })
                mb.Post (Some b) })
            |> Async.map (fun _ -> mb.Post None)
            |> Async.StartChildAsTask
        yield!
            AsyncSeq.unfoldAsync (fun (t:Task) -> async{
            if t.IsFaulted then
                return None
            else
                let! d = mb.Receive()
                match d with
                | Some c ->
                    let! d' = c
                    return Some (d',t)
                | None -> return None
            })
            err
    }

    ///Invoke f in parallel while maintaining the tokens per minute rate.
    ///Input is a sequence of (tokens:unint64 *'a) where the tokens is the number of input tokens associated with value 'a.
    ///Note: ordering is not maintained
    let mapAsyncParallelTokenLimit (tokensPerMinute:float) (f:'a -> Async<'b>) (s:AsyncSeq<uint64*'a>) : AsyncSeq<'b> = 
        _mapAsyncParallelUnits "tokens" tokensPerMinute f s

    ///Invoke f in parallel while maintaining opsPerSecond rate.
    ///Note: ordering is not maintained
    let mapAsyncParallelRateLimit (opsPerSecond:float) (f:'a -> Async<'b>) (s:AsyncSeq<'a>) : AsyncSeq<'b> =
        _mapAsyncParallelUnits "ops" (opsPerSecond * 60.0) f (s |> AsyncSeq.map (fun a -> 1UL,a))

