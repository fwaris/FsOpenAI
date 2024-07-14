#load "packages.fsx"
open System
open FSharp.Interop.Excel
open FSharp.Data
type TMeta = CsvProvider<"Type,Name,Label",Schema="Type,Name,Label">

let [<Literal>] CodebookFile  = @"E:\s\nhts\csv\codebook.xlsx"

type Cbhh = ExcelFile<CodebookFile, SheetName="Household", ForceString=true>
type CbVeh = ExcelFile<CodebookFile, SheetName="Vehicle", ForceString=true>
type CbPer = ExcelFile<CodebookFile, SheetName="Person", ForceString=true>
type CbTrip = ExcelFile<CodebookFile, SheetName="Trip", ForceString=true>
type CbLdt = ExcelFile<CodebookFile, SheetName="Long Distance", ForceString=true>

let metaData = 
    seq {
        yield! Cbhh().Data |> Seq.filter(_.Name>>String.IsNullOrWhiteSpace>>not) |> Seq.map(fun x -> "HH", x.Name, x.Label) 
        yield! CbVeh().Data |> Seq.filter(_.Name>>String.IsNullOrWhiteSpace>>not) |> Seq.map(fun x -> "Veh", x.Name, x.Label) 
        yield! CbPer().Data |> Seq.filter(_.Name>>String.IsNullOrWhiteSpace>>not) |> Seq.map(fun x -> "Per", x.Name, x.Label) 
        yield! CbTrip().Data |> Seq.filter(_.Name>>String.IsNullOrWhiteSpace>>not) |> Seq.map(fun x -> "Trip", x.Name, x.Label) 
        yield! CbLdt().Data |> Seq.filter(_.Name>>String.IsNullOrWhiteSpace>>not) |> Seq.map(fun x -> "Ldt", x.Name, x.Label)
    }
    |> Seq.map TMeta.Row

let metaFile = @"E:\s\nhts\csv\metaData.csv"

(new TMeta(metaData)).Save(metaFile)

let byName = 
    metaData
    |> Seq.groupBy(_.Name)
    |> Seq.map(fun (x,ys) -> x, ys |> Seq.map(_.Type) |> Seq.distinct |> Seq.toList)
    |> Seq.toList

let dupes = byName |> List.filter(fun (_,xs) -> xs.Length > 1)
dupes.Length
byName.Length
dupes |> List.iter (fun (x,ys) -> printfn "%s %A" x ys)



    
