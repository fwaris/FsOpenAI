#load "packages.fsx"
open System
open FSharp.Interop.Excel
open FSharp.Data
type TMeta = CsvProvider<"Type,Name,Label",Schema="Type,Name,Label">
type TMetaCompact = CsvProvider<"Name,Label,Datasets",Schema="Name,Label,Datasets">

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

let byName = 
    metaData
    |> Seq.groupBy(_.Name)
    |> Seq.map(fun (x,ys) -> x, ys |> Seq.map(_.Type) |> Seq.distinct |> Seq.toList, (Seq.head ys).Label)
    |> Seq.toList

let compactMeta = 
    byName
    |> Seq.map(fun (x,ys,z) -> x, z, ys |> String.concat ", ")
    |> Seq.map TMetaCompact.Row

let metaFile = @"E:\s\nhts\csv\metaDataCompact.csv"

(new TMetaCompact(compactMeta)).Save(metaFile)


let dupes = byName |> List.filter(fun (_,xs,_) -> xs.Length > 1)
dupes.Length
byName.Length
dupes |> List.iter (fun (x,ys,_) -> printfn "%s %A" x ys)




    
