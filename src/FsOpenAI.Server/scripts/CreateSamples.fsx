#load "Env.fsx"
open FsOpenAI.Client
open System.IO
open System.Text.Json
open System.Text.Json.Serialization


let saveSamples (samples:SamplePrompt list) (file:string) =
    let folder = Path.GetDirectoryName(file)
    if Directory.Exists folder |> not then Directory.CreateDirectory folder |> ignore    
    let json = JsonSerializer.Serialize(samples,options=Utils.serOptions())
    System.IO.File.WriteAllText(file,json)

let samples =
    let m1 = ["gpt-4"; "gpt-3.5-turbo"]
    let m2 =  ["gpt-4-32k"; "gpt-3.5-turbo-16k"]

    [
        {
            SampleChatType = Simple_Chat true
            Temperature = 0.1
            MaxDocs     = 5
            PreferredModels = m2
            SystemMessage  = "You are a helpful AI assistant"
            SampleQuestion = """Analyze the latest trend in Microsoft stock price"""
        }
        {
            SampleChatType = Simple_Chat false
            Temperature = 0.1
            MaxDocs     = 5
            PreferredModels = m2
            SystemMessage  = "You are a qualified accountant"
            SampleQuestion = """What is cost of capital and how to estimate it accurately?"""
        }
        {
            SampleChatType = QA_Chat "verizon-sec"
            SystemMessage  = "You are a helpful AI Assistant"
            SampleQuestion = """List the non GAAP related policy decisions that Verizon disclosed in its SEC filings"""
            Temperature = 0.1
            MaxDocs = 30
            PreferredModels = m2
        }
        {
            SampleChatType = DocQA_Chat "verizon-sec"
            SystemMessage  = "You are a helpful AI Assistant"
            SampleQuestion = """How does the content in the DOCUMENT affect the policies in SEARCH RESULTS?"""
            Temperature = 0.1
            MaxDocs = 30
            PreferredModels = m2
        }
    ]

let fn = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"/../wwwroot/Templates/Finance/Samples.json")

saveSamples samples fn 







    


