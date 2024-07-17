#load "../../FsOpenAI.Tasks/scripts/ScriptEnv.fsx"
#load "packages.fsx"

open FsOpenAI.Shared.Interactions
open FsOpenAI.Shared
open FsOpenAI.GenAI
open System.IO
open FSharp.Interop.Excel

let dispatch (m:ServerInitiatedMessages) = ()
let invCtx = InvocationContext.Default
ScriptEnv.installSettings @"%USERPROFILE%\.fsopenai/openai/ServiceSettings.json"
let settings = ScriptEnv.settings.Value

let codeFile = __SOURCE_DIRECTORY__ + "/gen.py"

//nhts files
let [<Literal>] CodebookFile = @"e:/s/nhts/csv/codebook.xlsx"

//typed representation of the nhts codebook sheets
type Cbhh = ExcelFile<CodebookFile, SheetName="Household", ForceString=true>
type CbVeh = ExcelFile<CodebookFile, SheetName="Vehicle", ForceString=true>
type CbPer = ExcelFile<CodebookFile, SheetName="Person", ForceString=true>
type CbTrip = ExcelFile<CodebookFile, SheetName="Trip", ForceString=true>
type CbLdt = ExcelFile<CodebookFile, SheetName="Long Distance", ForceString=true>

let dataFolder = @"E:\s\nhts\csv"

//indexed names and types of the nhts files
let hhCb = (Cbhh(CodebookFile).Data)
let vehCb = (CbVeh(CodebookFile).Data)
let perCb = (CbPer(CodebookFile).Data)
let tripCb = (CbTrip(CodebookFile).Data)
let ldtCb = (CbLdt(CodebookFile).Data)

let inline toMarkdown (xs:^t seq) =
    let hdr = ["Name"; "Label"; "Type"; "Length"; "Codes"; "Frequency"; "Weighted"] |> String.concat "|"
    let hdr = $"|{hdr}|\n|---|---|---|---|---|---|---|\n"
    xs
    |> Seq.map (fun x -> 
        let name = ((^t) : (member Name : string) (x))                 //note: complier will enforce that
        let label = ((^t) : (member Label : string) (x))               //type ^t implements these members,
        let typ = ((^t) : (member Type : string) (x))                  //at the point where this function is called
        let length = ((^t) : (member Length : string) (x))
        let code = ((^t) : (member ``Code / Range`` : string) (x))
        let freq = ((^t) : (member Frequency : string) (x))
        let weighted = ((^t) : (member Weighted : string) (x))
        let line = [name; label; typ; length; code; freq; weighted] |> String.concat "|"
        $"|{line}|")
    |> Seq.toList

let md = 
    [
        ["# Codebook for Households"]
        hhCb |> toMarkdown
        ["# Codebook for Vehicles"]
        vehCb |> toMarkdown
        ["# Codebook for Persons"]
        perCb |> toMarkdown
        ["# Codebook for Trips"]
        tripCb |> toMarkdown
        ["# Codebook for Long Trips"]
        ldtCb |> toMarkdown
        [
            "# CSV File Paths"
            "- Household: e:/s/nhts/csv/hhv2pub.csv"
            "- Vehicle: e:/s/nhts/csv/vehv2pub.csv"
            "- Person: e:/s/nhts/csv/perv2pub.csv"
            "- Trip: e:/s/nhts/csv/tripv2pub.csv"
            "- Long Trips: e:/s/nhts/csv/ldtv2pub.csv"
        ]
    ]
    |> List.concat
    |> String.concat "\n"

let sysMsg = """
You are an AI assistant that can generate Python code to answer a user QUERY on the NHTS dataset consisting of several CSV files. Think step-by-step to generate valid Python code that answers the user query.
You may reference well-know Python libraries like pandas, numpy, etc. to help you generate the code.
The dtypes for call columns in the csv files are numeric (e.g. int, int64 or float)
"""

let codePrompt question = $"""
QUERY```
{question}
```
{md}

```python
"""
;;
let questions =  
    [
    "What is the average commute time for workers in the United States?"
    "What percentage of times people carpool together for work?"
    "What percentage of households own electric vehicles (EVs)?"
    "What are the most common reasons for travel during weekends?"
    "What modes of transportation do college students use to get to campus?. Rank each by share"
    "What percentage of households have Ford vehicles?"
    //
    "What is the percentage of trips by vehicle type?"
    "What percentage of persons used rideshare in the last 30 days. Present the data by Census region." 
    "What is the distribution of riders per trip?"
    "What is the average length of trips by mode of transportation?"
    "What percentage of the trips are loop trips?"
    "What is the average amount paid for parking per trip by census region? Calclulate only for trips where parking was paid"
    "What is the max amount paid for parking per trip by census region?"
    ]

let question = questions.[1]

let chPlan =
    Interaction.create InteractionCreateType.Crt_Plain OpenAI None
    |> snd
    |> Interaction.setSystemMessage (sysMsg)
    |> Interaction.setUserMessage (codePrompt question)

let resp = Completions.completeChat settings invCtx chPlan None dispatch |> ScriptEnv.runA
let code = GenUtils.extractCode resp.Content
printfn "%s" code
File.WriteAllText(codeFile, code)
