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

type Codebook = {Name:string; Typ:string; Length:int; Label:string; Codes:string list; CodeSet:Set<string>; Frequency:float; Weighted:float}

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
        let codes = vs |> List.map (fun x -> x.code) |> List.filter (isEmpty>>not) 
        let codeSet = codes |> List.map (_.ToLower()) |> set
        let v = vs.Head
        {Name=v.name; Label=v.label; Typ=v.typ; Length= int v.length; Codes=codes; CodeSet=codeSet; Frequency=v.freq; Weighted=v.weighted})

let baseResponseType() = 
    let typeDef = """
type Response = 
    | R_NotAscertained
    | R_Skipped
    | Value of float
"""
    "Response", Some typeDef

let baseYesNoType() = 
    let typeDef = """
type YesNo = 
    | YN_NotAscertained
    | YN_Skipped
    | Yes
    | No
"""
    "YesNo", Some typeDef

let enumName (s:string) = 
    s.Split("_").[0]

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
    if markers |> Set.exists (fun x -> cs |> List.exists (fun (a,b) -> a = x)) then 
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

let (|OtherEum|_|) (codebook:Codebook) = 
    let cs = codes codebook
    let unionCases = cs |> List.choose snd |> List.map(fun x -> if isIdentifier x  then  $"| {capitalizeFirst x}" else $"| `{x}`")
    let enumName = enumName codebook.Name
    let typeDef =
        seq {
            yield $"type {enumName} ="
            yield! unionCases
        }
        |> String.concat "\n"
    Some (enumName, Some typeDef)
    
let (|Numeric|_|) (codebook:Codebook) = 
    let cs = codes codebook |> List.choose snd 
    if codebook.Typ = "N" && cs.Length <= 1 then 
        Some ("float", None)
    else
        None

let (|Identifier|_|) (codebook:Codebook) = 
    if codebook.Typ = "C" && codebook.Codes.Length <= 1 && codebook.Length = 10 then 
        Some ("int64", None)
    else
        None
let (|CharIdentifier|_|) (codebook:Codebook) = 
    if codebook.Typ = "C" && codebook.Codes.Length <= 10 && codebook.Length <= 10 then 
        Some ("string", None)
    else
        None

let deriveType cache (codebook:Codebook) =
    cache
    |> Map.tryFind codebook.CodeSet
    |> Option.map (fun x -> cache,x)
    |> Option.orElseWith (fun _ -> 
        printfn $"{codebook.Name}: {codebook.Codes}"
        match codebook with
        | CharIdentifier x  -> x
        | Identifier x      -> x
        | Numeric x         -> x
        | BaseReponse x     -> x
        | YesNo x           -> x
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


let cb1 = perCb |> List.find(fun x->x.Name="PERSONID")
let (i : (string*string option) option)= (|CharIdentifier|_|) cb1

let typeDefs = 
    (Map.empty,List.concat [hhCb;vehCb;perCb;tripCb;ldtCb]) 
    ||> List.fold(fun cache d -> deriveType cache d |> fst)

typeDefs |> Map.toSeq |> Seq.choose (snd>>snd) |> Seq.iter (printfn "%s")


//generate a record type from the given headers, types and descriptions
let genRec name (headers:Map<string,int>) (types:Map<int,string>) (descs:Map<string,string>) (codebook:Codebook) = 
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

