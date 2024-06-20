module FsOpenAI.TravelSurvey.Data
open FsOpenAI.TravelSurvey.Types
open FsOpenAI.TravelSurvey.Loader
open System.IO
open FSharp.Data

let private (@@) (a:string) (b:string) = Path.Combine(a,b)

let loadFromPath (path: string) : DataSets =
    let HHFile = path @@ "hhv2pub.csv"
    let VEHFile = path @@ "vehv2pub.csv"
    let PERFile = path @@ "perv2pub.csv"
    let TRIPFile = path @@ "tripv2pub.csv"
    let LDTFile = path @@ "ldtv2pub.csv"

    let hhData = CsvFile.Load(HHFile).Rows |> Seq.map (fun x -> [|0 .. x.Columns.Length - 1|] |> Array.map (fun i -> x.[i]) ) |> Seq.map to_Household
    let vehData = CsvFile.Load(VEHFile).Rows |> Seq.map (fun x -> [|0 .. x.Columns.Length - 1|] |> Array.map (fun i -> x.[i]) ) |> Seq.map to_Vehicle
    let perData = CsvFile.Load(PERFile).Rows |> Seq.map (fun x -> [|0 .. x.Columns.Length - 1|] |> Array.map (fun i -> x.[i]) ) |> Seq.map to_Person
    let tripData = CsvFile.Load(TRIPFile).Rows |> Seq.map (fun x -> [|0 .. x.Columns.Length - 1|] |> Array.map (fun i -> x.[i]) ) |> Seq.map to_Trip
    let ldtData = CsvFile.Load(LDTFile).Rows |> Seq.map (fun x -> [|0 .. x.Columns.Length - 1|] |> Array.map (fun i -> x.[i]) ) |> Seq.map to_LongTrip
    
    { 
        Household = hhData |> Seq.toList
        Vehicle = vehData |> Seq.toList
        Person = perData |> Seq.toList
        Trip = tripData |> Seq.toList
        LongTrip = ldtData |> Seq.toList
    }


(* to debug as exe

let path = @"E:\s\nhts\csv"

let data = loadFromPath path
//lengths
printfn "Household: %d" data.Household.Length
printfn "Vehicle: %d" data.Vehicle.Length
printfn "Person: %d" data.Person.Length
printfn "Trip: %d" data.Trip.Length
printfn "LongTrip: %d" data.LongTrip.Length
let i = 0

*)
