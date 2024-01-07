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
    printfn $"saved samples {Path.GetFullPath(file)}"


module Finance =
    let SamplesPath = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"../../../FsOpenAI.Server/wwwroot/app/Templates/Finance/Samples.json")
    let samples =
        let m1 = ["gpt-4"; "gpt-3.5-turbo"]
        let m2 =  ["gpt-4-32k"; "gpt-3.5-turbo-16k"]

        [
            {
                SampleChatType = Simple_Chat true
                SampleMode = ExplorationMode.Factual
                MaxDocs     = 5
                PreferredModels = m1
                SampleSysMsg  = "You are a helpful AI assistant"
                SampleQuestion = """Analyze the latest trend in T-Mobile stock price"""
            }
            {
                SampleChatType = Simple_Chat false
                SampleMode = ExplorationMode.Factual
                MaxDocs     = 5
                PreferredModels = m1
                SampleSysMsg  = "You are a qualified accountant"
                SampleQuestion = """Does the amount in a PO exist on the GL prior to the actual invoices coming in when working on an accrual basis? What I'm trying to decipher is what the benefit would be, if any, to close out a PO that didn't exhaust the full amount entitled to it. Is there any effect on the BS or the P&L?"""
            }
            {
                SampleChatType = QA_Chat "verizon-sec"
                SampleSysMsg  = "You are a helpful AI Assistant"
                SampleQuestion = """List the 'non-GAAP' related policy decisions that are disclosed in the Verizon SEC filings"""
                SampleMode = ExplorationMode.Factual
                MaxDocs = 30
                PreferredModels = m2
            }
            {
                SampleChatType = DocQA_Chat "accounting-policy"
                SampleSysMsg  = "You are a helpful AI Assistant"
                SampleQuestion = """How does the content in the DOCUMENT affect the policies in SEARCH RESULTS?"""
                SampleMode = ExplorationMode.Factual
                MaxDocs = 30
                PreferredModels = m2
            }
        ]

(*
saveSamples Finance.samples Finance.SamplesPath 
*)

