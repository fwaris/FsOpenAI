#load "packages.fsx"
open System
open System.IO 
open FSharp.Data
open FSharp.Interop.Excel

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

(*
["a"; "a"; "a"; "b"; "c"; "c"] |> Seq.groupAdjacent (fun (a,b)->a=b)
val it : seq<seq<string>> = seq [["a"; "a"; "a"]; ["b"]; ["c"; "c"]]
*)

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

//map of field name to field description
let descs = 
    TData(TypesFile).Data
    |> Seq.map (fun x -> x.NAME, x.LABEL)
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

type CodeBook = {Name:string; Typ:string; Length:int; Label:string; Codes:string list; Frequency:float; Weighted:float}

let isEmpty (s:string) = String.IsNullOrWhiteSpace s

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
        let name = ((^t) : (member Name : string) (x))
        let label = ((^t) : (member Label : string) (x))
        let typ = ((^t) : (member Type : string) (x))
        let length = ((^t) : (member Length : string) (x))
        let code = ((^t) : (member ``Code / Range`` : string) (x))
        let freq = ((^t) : (member Frequency : string) (x))
        let weighted = ((^t) : (member Weighted : string) (x))
        {|name=name; label=label;typ=typ;length=length |> toInt; code=code; freq=freq |> toFloat; weighted=weighted |> toFloat|})
    |> Seq.toList
    |> Seq.groupAdjacent (fun (a,b) -> (isEmpty a.name && isEmpty b.name) || (not (isEmpty a.name)  && isEmpty b.name))
    |> List.map (fun vs -> 
        let codes = vs |> List.map (fun x -> x.code)
        let v = vs.Head
        {Name=v.name; Label=v.label; Typ=v.typ; Length= int v.length; Codes=codes; Frequency=v.freq; Weighted=v.weighted})

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


//generate a record type from the given headers, types and descriptions
let genRec name (headers:Map<string,int>) (types:Map<int,string>) (descs:Map<string,string>)= 
    seq {
        yield $"type {name} = {{"
        yield!
            headers 
            |> Map.toSeq
            |> Seq.sortBy snd
            |> Seq.map(fun (h,i) -> 
                let t = 
                    match types.TryFind i with
                    | Some t -> t
                    | None -> "obj"
                let d = 
                    match descs.TryFind h with
                    | Some d -> d
                    | None -> ""
                $"    {h} : {t} // {d}")
        yield "}"
    }
    |> String.concat "\n"

let recType = function 
    | "Int32" -> "int"
    | "String" -> "string"
    | "Decimal" -> "float"
    | "Int64" -> "int64"

//record types for the nhts files
genRec "Household" hh hhT descs
genRec "Vehicle" veh vehT descs
genRec "Person" per perT  descs
genRec "Trip" trip tripT   descs
genRec "Tldt" ldt ldtT  descs

(*
[System]
You are a helpful AI Assistant
[User]
find the list of all categorial variable and list them with their categories and associated code values
[Assistant]
Based on the provided document, here is a list of all categorical variables along with their categories and associated code values:

### Trip Purpose Variables (2022 and 2017 NHTS)
#### 2022 Trip Purpose Codes:
- **-9**: Not ascertained
- **-8**: Don’t know
- **-7**: Refused
- **1**: Regular activities at home
- **2**: Work from home
- **3**: Work at a non-home location
- **4**: Work activity to drop-off/pickup someone/something
- **5**: Other work-related activities
- **6**: Attend school as a student
- **7**: Attend child or adult care
- **8**: Volunteer activities
- **9**: Change transportation type
- **10**: Drop off/pick up someone
- **11**: Health care visit
- **12**: Buy meals
- **13**: Shop/buy/pick-up or return goods
- **14**: Other family/personal errands
- **15**: Recreational activities
- **16**: Exercise
- **17**: Visit friends or relatives
- **18**: Religious or other community activities
- **19**: Rest or relaxation/vacation
- **97**: Something else

#### 2017 Trip Purpose Codes:
- **-9**: Not ascertained
- **-8**: Don’t know
- **-7**: Refused
- **1**: Reg. home activities
- **2**: Work from home
- **3**: Work
- **4**: Work related/Trip
- **5**: Volunteer activities
- **6**: Drop off/pickup someone
- **7**: Change type of transportation
- **8**: Attend school as a student
- **9**: Attend child care
- **10**: Attend adult care
- **11**: Buy goods
- **12**: Buy services
- **13**: Buy meals
- **14**: Other general errands
- **15**: Recreational activities
- **16**: Exercise
- **17**: Visit friends and relatives
- **18**: Health care visit
- **19**: Religious or other community activities
- **97**: Something else

### Means of Transportation Codes (2022 and 2017 NHTS)
#### 2022 Means of Transportation Codes:
- **01**: Car
- **02**: Van
- **03**: SUV/Crossover
- **04**: Pick Up Truck
- **05**: Van (Minivan)
- **06**: Recreational Vehicle
- **07**: Motorcycle
- **08**: Motorcycle/Moped
- **09**: RV (Motorhome, ATV, Snowmobile)
- **10**: Streetcar or trolley car
- **11**: Subway or Elevated Rail
- **12**: Commuter Rail
- **13**: Private/Charter/Tour/Shuttle Bus
- **14**: Airplane
- **15**: Taxicab or limo service
- **16**: Other ride-sharing services
- **17**: Paratransit/Dial-a-Ride
- **18**: Bicycle (including bikeshare, ebike, etc.)
- **19**: E-scooter
- **20**: Walk
- **21**: Other
- **22**: Ferryboat

#### 2017 Means of Transportation Codes:
- **01**: Walk
- **02**: Bicycle
- **03**: Car
- **04**: SUV
- **05**: Van (Minivan)
- **06**: Pick Up Truck
- **07**: Golf cart/Segway
- **08**: Motorcycle/Moped
- **09**: School bus
- **10**: Streetcar or trolley car
- **11**: Public or Commuter Bus
- **12**: Paratransit/Dial-a-Ride
- **13**: Amtrak
- **14**: City-to-City Bus (Greyhound, Megabus)
- **15**: Amtrak/Commuter Rail
- **16**: Subway/Elevated/Light Rail/Streetcar
- **17**: Taxi/Limo (including Uber/Lyft)
- **18**: Rental Car (Inc. Zipcar and Car2Go)
- **19**: Airplane
- **20**: Boat/Ferry/Water Taxi
- **97**: Something else
*)
