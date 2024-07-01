#load "packages.fsx"
open System
open System.IO 
open FSharp.Data
open FSharp.Interop.Excel

let ignoreCase = StringComparison.OrdinalIgnoreCase

module Seq =
    let groupAdjacent f input = 
        let prev = Seq.head input
        let sgs,n = 
            (([],prev),input |> Seq.tail)
            ||> Seq.fold(fun (acc,prev) next -> 
                if f (prev,next) then 
                    match acc with
                    | [] -> [[prev]],next
                    | xs::rest -> (prev::xs)::rest,next
                else
                    match acc with 
                    | [] -> [[];[prev]],next
                    | xs::rest -> []::(prev::xs)::rest,next)
        let sgs = 
            match sgs with 
            | [] -> [[n]]
            | xs::rest -> 
                match xs with
                | [] -> [n]::rest
                | ys -> (n::ys)::rest
        sgs |> List.map List.rev |> List.rev

let (@@) (a:string) (b:string) = Path.Combine(a,b)

let [<Literal>] folder = @"e:/s/nhts/csv"

let files = Directory.GetFiles folder

//nhts files
let [<Literal>] TypesFile = @"e:/s/nhts/csv/dictionary.xlsx"
let [<Literal>] CodebookFile = @"e:/s/nhts/csv/codebook.xlsx"
let [<Literal>] HHFile = @"e:/s/nhts/csv/hhv2pub.csv"
let [<Literal>] VEHFile = @"e:/s/nhts/csv/vehv2pub.csv"
let [<Literal>] PERFile = @"e:/s/nhts/csv/perv2pub.csv"
let [<Literal>] TRIPFile = @"e:/s/nhts/csv/tripv2pub.csv"
let [<Literal>] LDTFile = @"e:/s/nhts/csv/ldtv2pub.csv"

//typed representation of the nhts files
type TData = ExcelFile<TypesFile>
type Cbhh = ExcelFile<CodebookFile, SheetName="Household", ForceString=true>
type CbVeh = ExcelFile<CodebookFile, SheetName="Vehicle", ForceString=true>
type CbPer = ExcelFile<CodebookFile, SheetName="Person", ForceString=true>
type CbTrip = ExcelFile<CodebookFile, SheetName="Trip", ForceString=true>
type CbLdt = ExcelFile<CodebookFile, SheetName="Long Distance", ForceString=true>
type Thh = CsvProvider<HHFile, InferRows=3000>
type Tveh = CsvProvider<VEHFile, InferRows=3000>
type TPer = CsvProvider<PERFile, InferRows=3000>
type Ttrip = CsvProvider<TRIPFile, InferRows=3000>
type Tldt = CsvProvider<LDTFile, InferRows=3000>

type Codebook = {Name:string; Typ:string; Length:int; Label:string; Codes:string list; CodeSet:Set<string>; Frequency:float; Weighted:float}
type TypeDef = {TypeName:string; TypeDef:string option; Converter:string} 

let isEmpty (s:string) = String.IsNullOrWhiteSpace s

let hasKeyWrdResponses (codebook:Codebook) = 
    codebook.Codes |> List.exists(fun x -> x.Contains("Responses=", ignoreCase))
let removeLines (s:string) = if isEmpty s then "" else  s.Replace("\n"," ").Replace("  "," ")

//map of field name to field description
let descs = 
    TData(TypesFile).Data
    |> Seq.map (fun x -> x.NAME, removeLines x.LABEL)
    |> Map.ofSeq

//return the give file headers as a map of header name to index
let indexHeaders (headers:string[] option) = 
    match headers with
    | Some h -> h |> Array.indexed |> Array.map (fun (i,h) -> h,i)
    | None -> [||]
    |> Map.ofSeq

//return the inferred types of the fields of 't which should tuple type (i.e. a CsvProvider Row type)
let indexTypes<'t>() = 
    Reflection.FSharpType.GetTupleElements(typeof<'t>) 
    |> Seq.map(fun x -> x.Name)     
    |> Seq.indexed
    |> Seq.map (fun (i,h) -> i,h)
    |> Map.ofSeq

let toInt (s:string) = 
    match Int32.TryParse s with
    | true, i -> i
    | _ -> 0

let toFloat (s:string) =     
    match Double.TryParse s with
    | true, f -> f
    | _ -> 0.0

let inline codeBook (xs:^t seq) =
    xs
    |> Seq.map (fun x -> 
        let name = ((^t) : (member Name : string) (x))                 //note: complier will enforce that
        let label = ((^t) : (member Label : string) (x))               //type ^t implements these members,
        let typ = ((^t) : (member Type : string) (x))                  //at the point where this function is called
        let length = ((^t) : (member Length : string) (x))
        let code = ((^t) : (member ``Code / Range`` : string) (x))
        let freq = ((^t) : (member Frequency : string) (x))
        let weighted = ((^t) : (member Weighted : string) (x))
        {|name=name; label=label;typ=typ;length=length |> toInt; code=code; freq=freq |> toFloat; weighted=weighted |> toFloat|})
    |> Seq.toList
    |> Seq.groupAdjacent (fun (a,b) -> (isEmpty a.name && isEmpty b.name) || (not (isEmpty a.name)  && isEmpty b.name))
    |> List.map (fun vs -> 
        let codes = vs |> List.map (fun x -> x.code) |> List.filter (isEmpty>>not) 
        let codeSet = codes |> List.map (_.ToLower()) |> set
        let v = vs.Head
        {Name=v.name; Label=v.label; Typ=v.typ; Length= int v.length; Codes=codes; CodeSet=codeSet; Frequency=v.freq; Weighted=v.weighted})

let baseResponseType() = 
    let typeDef = """type Response = 
    | R_NotAscertained
    | R_Skipped
    | Value of float"""
    {TypeName = "Response"; TypeDef=  Some typeDef; Converter="toBaseResponse"}

let baseYesNoType() = 
    let typeDef = """type YesNo = 
    | YN_NotAscertained
    | YN_Skipped
    | ``YN_Don't Know``
    | Yes
    | No"""
    {TypeName = "YesNo"; TypeDef=  Some typeDef; Converter="toYesNo"}

let normalizedTypeName (s:string) = if s.StartsWith("ONTP_P") then "ONTP_P" else s

let codes codebook = 
    codebook.Codes 
    |> List.map (fun x -> 
        let ps =  x.Split('=')
        let p0 = ps.[0].Trim()
        let p0 = match Int32.TryParse p0 with | true,v -> Some v | _ -> None
        (p0, if ps.Length > 1 then Some (ps.[1].Trim()) else None))

let (|BaseReponse|_|) (codebook:Codebook) = 
    let cs = codes codebook 
    let markers = [-1;-9] |> List.map Some |> set
    let codes = cs |> List.choose snd
    let hasMarkers = markers |> Set.exists (fun x -> cs |> List.exists (fun (a,b) -> a = x))
    let hasKeyWrdResponses = hasKeyWrdResponses codebook
    let hasNonMarkerEnums = 
        cs
        |> List.filter (fun (i,_) -> markers.Contains i |> not)
        |> List.choose snd
        |> List.length > 0
    if hasMarkers && (hasKeyWrdResponses || not hasNonMarkerEnums) then 
        Some (baseResponseType())
    else
        None

let (|YesNo|_|) (codebook:Codebook) = 
    let cs = codes codebook |> List.choose snd
    if 
        cs |> List.exists(fun x -> x.Equals("Yes",ignoreCase)) 
        && cs |> List.exists(fun x -> x.Equals("No",ignoreCase)) 
        && cs.Length <= 4 then
            baseYesNoType() |> Some
        else
            None

let isIdentifier (s:string) = 
    s.Length > 0
    && not(Char.IsDigit s.[0])
    && s |> Seq.forall (fun c -> Char.IsLetterOrDigit c || c = '_')

let capitalizeFirst (s:string) =
    let h,t = Seq.head s, Seq.tail s
    seq {
        yield (Char.ToUpper(h))
        yield! t
    }
    |> String.Concat

let legalize (s:string) = 
    s.Replace("\\","|")
        .Replace("+"," ")
        .Replace("&","")
        .Replace("$","")
        .Replace("/","|")
        // .Replace(",","")
        .Replace(".","")
        .Trim()

let unionCase n (s:string) =
    if isIdentifier s then 
        // $"{n}_{capitalizeFirst s}"
        $"{capitalizeFirst s}"
    else 
        let l = s |> legalize 
        // $"``{n}_{l}``"
        $"``{l}``"

let (|OtherEum|_|) (codebook:Codebook) = 
    let enumName = normalizedTypeName codebook.Name
    let cs = codes codebook
    let unionCases = cs |> List.choose snd |> List.map (fun s -> $"| {unionCase enumName  s}")
    let typeDef =
        seq {
            yield $"/// {codebook.Label}"
            yield $"type {enumName} ="
            yield! unionCases
        }
        |> String.concat "\n"
    Some {TypeName = enumName; TypeDef= Some typeDef; Converter= "to" + enumName}
    
let (|Numeric|_|) (codebook:Codebook) = 
    let cs = codes codebook |> List.choose snd 
    if codebook.Typ = "N" && cs.Length <= 1 then 
        Some {TypeName = "float"; TypeDef= None; Converter="toFloat"}
    else
        None

let (|CharNumeric|_|) (codebook:Codebook) = 
    let cs = codes codebook |> List.choose snd 
    if codebook.Typ = "C" && cs.Length = 1 && hasKeyWrdResponses codebook  then 
        Some {TypeName = "float"; TypeDef= None; Converter="toFloat"}
    else
        None

let (|Identifier|_|) (codebook:Codebook) = 
    if codebook.Typ = "C" && codebook.Codes.Length <= 1 && codebook.Length >= 10 then 
        Some {TypeName = "string"; TypeDef= None; Converter="string"}        
    else
        None

let (|CharIdentifier|_|) (codebook:Codebook) = 
    if codebook.Typ = "C" && codebook.Codes.Length <= 50 && codebook.Length <= 12 && codebook.Name.EndsWith("ID") then 
        Some {TypeName = "string"; TypeDef= None; Converter="string"}
    else
        None

let (|Date|_|) (codebook:Codebook) = 
    if codebook.Typ = "C" && codebook.Length <= 6 && codebook.Name.EndsWith("DATE") then 
        Some {TypeName = "DateTime"; TypeDef= None; Converter="toDateTime"}        
    else
        None

let deriveType cache (codebook:Codebook) =
    cache
    |> Map.tryFind codebook.CodeSet
    |> Option.map (fun x -> cache,x)
    |> Option.orElseWith (fun _ -> 
        printfn $"{codebook.Name}: {codebook.Codes}"
        match codebook with
        | Date x            -> x 
        | CharIdentifier x  -> x
        | Identifier x      -> x
        | Numeric x         -> x
        | CharNumeric x     -> x
        | YesNo x           -> x
        | BaseReponse x     -> x
        | OtherEum x        -> x
        | _                 -> failwith "unexpected type"
        |> fun typeDef -> 
            let cache = cache |> Map.add codebook.CodeSet typeDef
            Some(cache,typeDef))
    |> Option.get

//indexed names and types of the nhts files
let hh = Thh.Load(HHFile).Headers  |> indexHeaders 
let hhT = indexTypes<Thh.Row>()
let hhCb = codeBook (Cbhh(CodebookFile).Data)

let veh = Tveh.Load(VEHFile).Headers |> indexHeaders
let vehT = indexTypes<Tveh.Row>()
let vehCb = codeBook (CbVeh(CodebookFile).Data)

let per = TPer.Load(PERFile).Headers |> indexHeaders
let perT = indexTypes<TPer.Row>()
let perCb = codeBook (CbPer(CodebookFile).Data)

let trip = Ttrip.Load(TRIPFile).Headers |> indexHeaders
let tripT = indexTypes<Ttrip.Row>()
let tripCb = codeBook (CbTrip(CodebookFile).Data)

let ldt = Tldt.Load(LDTFile).Headers |> indexHeaders
let ldtT = indexTypes<Tldt.Row>()
let ldtCb = codeBook (CbLdt(CodebookFile).Data)

let allCodebooks = 
    List.concat [hhCb;vehCb;perCb;tripCb;ldtCb]
    // |> List.filter (fun x -> x.Name<>"VEHTYPE") //manual override

let codeMap = allCodebooks |> List.map (fun x -> x.Name,x) |> Map.ofList

let typeDefs = 
    (Map.empty,allCodebooks) 
    ||> List.fold(fun cache d -> deriveType cache d |> fst)

//generate a record type from the given headers, types and descriptions
let genRec (typeDefs:Map<Set<string>,TypeDef>) (codeMap:Map<string,Codebook>) name (headers:Map<string,int>) (descs:Map<string,string>) = 
    seq {
        yield $"type {name} = {{"
        yield!
            headers 
            |> Map.toSeq
            |> Seq.sortBy snd
            |> Seq.map(fun (h,i) -> 
                let t = 
                    match codeMap |> Map.tryFind h with
                    | Some t -> typeDefs.[t.CodeSet]
                    | None -> failwith $"missing codebook for {h}"
                let d = 
                    match descs.TryFind h with
                    | Some d -> d
                    | None -> ""
                $"    {h} : {t.TypeName} // {d}")
        yield "}"
    }
    |> String.concat "\n"

let genReader (typeDefs:Map<Set<string>,TypeDef>) (codeMap:Map<string,Codebook>) name (headers:Map<string,int>) = 
    seq {
        yield $"let to_{name} (vals:string[]) ="
        yield " {"
        yield!
            headers 
            |> Map.toSeq
            |> Seq.sortBy snd
            |> Seq.map(fun (h,i) -> 
                let t = 
                    match codeMap |> Map.tryFind h with
                    | Some t -> typeDefs.[t.CodeSet]
                    | None -> failwith $"missing codebook for {h}"
                $"        {h} = {t.Converter} vals.[{i}]")
        yield "}"
    }
    |> String.concat "\n"

let genConverter (typeDef:TypeDef) (codebook:Codebook) =     
    let cs = codes codebook |> List.filter (fun (x,y) -> x.IsSome && y.IsSome) |> List.map (fun (x,y) -> x.Value,y.Value)
    seq {
        yield $"let to{typeDef.TypeName} (v:string) : {typeDef.TypeName} ="
        yield "    match int v with"
        yield! 
            cs
            |> List.map (fun (x,y) -> 
                $"    | {x} -> {unionCase typeDef.TypeName y}")
    }
    |> String.concat "\n"

let convertibles = allCodebooks |> List.choose (fun x -> 
    match typeDefs.TryFind x.CodeSet with
    | Some t -> Some (t,x)
    | None -> None)

let baseConverter = """let toBaseResponse (v:string) : Response = 
    try 
        match Int32.TryParse v with 
        | true, v -> 
            match v with
            | -1 -> R_NotAscertained
            | -9 -> R_Skipped
            | x -> Value(float x)
        | _ ->
            match Double.TryParse v with 
            | true, v -> Value v
            | _ -> failwith $"Unable to convert value to float: {v}"
    with ex -> 
        failwith $"Value:{v}; Error: {ex.Message}" """

let yesNoConverter = """let toYesNo (v:string) : YesNo = 
    match int v with
    | -1 -> YN_NotAscertained
    | -9 -> YN_Skipped
    | -8 -> ``YN_Don't Know``
    | 1 -> Yes
    | 2 -> No
    | x -> failwith $"Unexpected value: {x} for yes/no" """

let floatConverter = """
let toFloat (v:string) : float = 
    match Double.TryParse v with
    | true, f -> f
    | _ -> 0.0"""

let dateTimeConverter = """
let toDateTime (v:string) : DateTime = 
    match DateTime.TryParseExact(v, "yyyyMM", null, System.Globalization.DateTimeStyles.None) with
    | true, d -> d
    | _ -> DateTime.MinValue"""

//two slightly different versions of VEHTYPE exist in codebook
//use unified version below
let vehType = """
/// Vehicle type
type VEHTYPE =
| ``VEHTYPE_Valid skip``
| ``VEHTYPE_Automobile|Car|Stationwagon``
| ``VEHTYPE_Van (Minivan|Cargo|Passenger)``
| ``VEHTYPE_SUV (Santa Fe, Tahoe, Jeep, etc)``
| ``VEHTYPE_Pickup Truck``
| ``VEHTYPE_Other Truck``
| ``VEHTYPE_Recreational vehicle (RV)|Motorhome``
| VEHTYPE_MotorcycleMoped
| ``VEHTYPE_Something else``"""

let vehTypeConverter = """
let toVEHTYPE (v:string) : VEHTYPE =
    match int v with
    | -1 -> ``VEHTYPE_Valid skip``
    | 1 -> ``VEHTYPE_Automobile|Car|Stationwagon``
    | 2 -> ``VEHTYPE_Van (Minivan|Cargo|Passenger)``
    | 3 -> ``VEHTYPE_SUV (Santa Fe, Tahoe, Jeep, etc)``
    | 4 -> ``VEHTYPE_Pickup Truck``
    | 5 -> ``VEHTYPE_Other Truck``
    | 6 -> ``VEHTYPE_Recreational vehicle (RV)|Motorhome``
    | 7 -> VEHTYPE_MotorcycleMoped
    | 97 -> ``VEHTYPE_Something else``"""

let dataSetType = """
type DataSets = {
    Household   : Household list // List of Household records
    Vehicle     : Vehicle list //List of Vehicle records for each Household
    Person      : Person list //List of Household members
    Trip        : Trip list //One record per Household member's travel day trip (If at least one trip is made) 
    LongTrip    : LongTrip list //Long trip taken by Household
}"""

let converters = 
    let exSet = set ["VEHTYPE";"Response";"YesNo";"float";"string";"DateTime"; "int64"]
    (convertibles 
    |> List.filter (fun (t,x) -> t.TypeDef.IsSome && not (exSet.Contains t.TypeName))
    |> List.distinctBy (fun (t,x) -> t.TypeName)
    |> List.map (fun (t,x) -> genConverter t x))
    @ [baseConverter; yesNoConverter; 
       floatConverter; dateTimeConverter;
       vehTypeConverter]

(*
let cb1 = vehCb |> List.find(fun x->x.Name="VEHTYPE")
let (i : TypeDef option) = (|OtherEum|_|) cb1
printfn "%A" i
typeDefs.[cb1.CodeSet].TypeDef |> Option.iter (printfn "%s")
typeDefs |> Map.toSeq |> Seq.choose (fun (n,x) ->x.TypeDef |> Option.map(fun y -> n,y)) |> Seq.iter (printfn "%A")
*)

let allCodeTypes = 
    let exSet = set ["VEHTYPE"]
    typeDefs
    |> Map.toSeq
    |> Seq.map snd
    |> Seq.distinctBy _.TypeName
    |> Seq.filter (fun x-> not (exSet.Contains x.TypeName))
    |> Seq.choose _.TypeDef
    |> String.concat "\n\n"

let helpers = """module Helpers =

    let formatNumber (f:float) : string = sprintf "%0.2f" f

    let formatNumberPercent (f:float) : string = sprintf "%0.2f%%" f
"""

let  typesAndModules = 
    seq {
        """
// This file was generated by the createTypes.fsx script. Do not edit this file directly.
namespace FsOpenAI.TravelSurvey.Types
open System"""
        allCodeTypes
        vehType
        genRec typeDefs codeMap "Household" hh descs
        genRec typeDefs codeMap "Vehicle" veh descs
        genRec typeDefs codeMap "Person" per descs
        genRec typeDefs codeMap "Trip" trip descs
        genRec typeDefs codeMap "LongTrip" ldt descs
        dataSetType
        helpers
    }
    |> String.concat "\n\n"

let allReaders = 
    seq {
        """
// This file was generated by the createTypes.fsx script. Do not edit this file directly.
module FsOpenAI.TravelSurvey.Loader
open System
open FsOpenAI.TravelSurvey.Types"""        
        yield! converters
        genReader typeDefs codeMap "Household" hh 
        genReader typeDefs codeMap "Vehicle" veh
        genReader typeDefs codeMap "Person" per
        genReader typeDefs codeMap "Trip" trip
        genReader typeDefs codeMap "LongTrip" ldt
    }
    |> String.concat "\n\n"

let dir = Path.GetDirectoryName( __SOURCE_DIRECTORY__ )
let typesFile = dir @@ "Types.fs"
File.WriteAllText(typesFile, typesAndModules)

let loaderFile = dir @@ "Loader.fs"
File.WriteAllText(loaderFile, allReaders)
