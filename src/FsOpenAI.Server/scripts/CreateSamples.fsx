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

module Accounting =
    let SamplesPath = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"/../wwwroot/Templates/Accounting/Samples.json")
    let samples =
        let m1 = ["gpt-4"; "gpt-3.5-turbo"]
        let m2 =  ["gpt-4-32k"; "gpt-3.5-turbo-16k"]

        [
            {
                SampleChatType = Simple_Chat true
                Temperature = 0.1
                MaxDocs     = 5
                PreferredModels = m1
                SystemMessage  = "You are a helpful AI assistant"
                SampleQuestion = """Analyze the latest trend in T-Mobile stock price"""
            }
            {
                SampleChatType = Simple_Chat false
                Temperature = 0.1
                MaxDocs     = 5
                PreferredModels = m1
                SystemMessage  = "You are a qualified accountant"
                SampleQuestion = """Does the amount in a PO exist on the GL prior to the actual invoices coming in when working on an accrual basis? What I'm trying to decipher is what the benefit would be, if any, to close out a PO that didn't exhaust the full amount entitled to it. Is there any effect on the BS or the P&L?"""
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
                SampleChatType = DocQA_Chat "asc280"
                SystemMessage  = "You are a helpful AI Assistant"
                SampleQuestion = """How does the content in the DOCUMENT affect the policies in SEARCH RESULTS?"""
                Temperature = 0.1
                MaxDocs = 30
                PreferredModels = m2
            }
        ]

module GC =
    let SamplesPath = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"/../wwwroot/Templates/gc/Samples.json")

    let samples =
        let m1 = ["gpt-4"; "gpt-3.5-turbo"]
        let m2 =  ["gpt-4-32k"; "gpt-3.5-turbo-16k"]

        [
            {
                SampleChatType = QA_Chat "ericsson-install, ericsson-gc-academy"
                Temperature = 0.0
                MaxDocs     = 5
                PreferredModels = m1
                SystemMessage  = """You are a helpful telecom cell site construction assistant.

Thoroughly explore all implications when generating a response. Consider all aspects.

Be factual in your responses and cite the sources. Ask if you are not sure.
 """
                SampleQuestion = "What is the T-Mobile Standard for AC power cable size (e.g. 2/0, 3/0, etc.) between the PPC and the E6160 regardless of the breaker being used?"
            }
            {
                SampleChatType = QA_Chat "ericsson-install, ericsson-gc-academy"
                SystemMessage  = """You are a helpful telecom cell site construction assistant.

Thoroughly explore all implications when generating a response. Consider all aspects.

Be factual in your responses and cite the sources. Ask if you are not sure.
 """
                SampleQuestion = """On which interface module do External PPC Alarms terminate?"""
                Temperature = 0.0
                MaxDocs = 15
                PreferredModels = m2
            }
        ]

(*
saveSamples Accounting.samples Accounting.SamplesPath 
saveSamples GC.samples GC.SamplesPath
*)

