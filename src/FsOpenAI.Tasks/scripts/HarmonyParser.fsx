#load "ScriptEnv.fsx"
open FsOpenAI.Shared
open System
open System.IO
open System.Text.Json
open FsOpenAI.Shared.Utils
open System.Text.Json.Serialization


let txt = """
<|channel|>analysis<|message|>We need to analyze the document. It's a subpoena duces tecum from Middlesex District Attorney's Office to [Company]. The main legal demands: produce records regarding Commonwealth v. John Doe, docket 2288CR99988. Specifically, subscriber information and call detail records for phone number (857)241-0000 for dates Oct 1, 2020 to Nov 6, 2020. Also produce a Keeper of Records affidavit. Must send certified copy prior to May 5, 2025. Also the subpoena demands appearance of [Company]'s custodian of records to appear before Superior Court on May 5, 2025 at 9:00 AM to give evidence and bring all books, papers, correspondence, documents in possession relating to the case. Also demands that records be mailed to Middlesex County Superior Court. So main demands: produce specified records, produce affidavit, appear in court, provide certified copies. Also the subpoena includes instructions for service. So answer: produce subscriber info and call detail records for specified period, produce Keeper of Records affidavit, provide certified copies to court, appear in court. Also the subpoena demands that [Company] provide records prior to May 5, 2025. So answer accordingly.<|end|>**Main legal demands in the subpoena**

1. **Production of specific records**  
   * All subscriber information and incoming‑ and outgoing‑call detail records for phone number **(555) 241‑0000** covering **October 1 2020 – November 6 2020** (Eastern Standard Time).  
   * Any other books, papers, correspondence, or written/printed documents in T‑Mobile’s possession, custody or control that relate to the case (Commonwealth v. John Doe, docket 2288CR99988).

2. **Affidavit of the Keeper of Records**  
   * A sworn statement confirming that the requested records have been collected, are true and complete, and were made in good faith.

3. **Certified copies to the court**  
   * Certified copies of the above records (and the affidavit) must be sent to the Middlesex County Superior Court – Woburn, Attn: Criminal Clerk’s Office, 200 Trade Center, Woburn, MA 01801, **by May 5 2025**.

4. **Appearance in court**  
   * The Custodian of Records (or a designated representative) is required to appear before the Superior Court on **May 5 2025 at 9:00 AM** to give evidence and bring the requested documents.

5. **Service instructions**  
   * The subpoena specifies how it may be served (hand, mail, or email) and requires the service to be returned to the court.

In short, the subpoena compels T‑Mobile to provide the specified call‑detail records and related documents, submit a Keeper‑of‑Records affidavit, deliver certified copies to the court by the deadline, and appear in court to testify.
"""

open FsOpenAI.GenAI.StreamParser
let p_channel : Parser<string> = pstring "<|channel|>"
let p_message : Parser<string> = pstring "<|message|>"
let p_analysis : Parser<string> = pstring "analysis"
let p_end : Parser<string> = pstring "<|end|>"

let p_thought (thought:Ref<string>) : Parser<string> = 
    map (function Some v -> thought.Value <- v; Some v | _ -> None) (accumTill p_end)

let harmonyExp thought =    
    p_ws .> p_channel .> p_ws .> p_analysis .> p_ws .> p_message
    .> (p_thought thought) .> p_strm_any_string

//expression to parse response json to extract citations and stream out the answer
let exp cits =
    p_ws .> p_brace1 .> p_ws
    .> p_citations .> p_ws .> p_colon .> p_ws
    .> (p_citations_list cits) .> p_ws
    .> p_comma .> p_ws
    .> p_answer .> p_ws .> p_colon .> p_ws
    .> p_strm_quoted_string .> p_ws .> p_brace2

let thought = ref ""
let test source =
    ((harmonyExp thought,(State.Empty,[])),source)
    ||> Seq.scan updateState
    |> Seq.collect (fun (_,(_,os)) -> List.rev os)
    |> Seq.iter (printfn "%s")

let testLines = 
    seq{
        use str = new StringReader(txt)
        let mutable line = str.ReadLine()
        while line <> null do   
            yield line
            line <- str.ReadLine()
    }
    |> Seq.toList

test testLines
printfn "%s" thought.Value
