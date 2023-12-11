#load "Env.fsx"
open System
open System.IO
open System.Web
open FsOpenAI.Client
open FsOpenAI.Client.Interactions

Env.installSettings "%USERPROFILE%/.fsopenai/ServiceSettings.json"
Env.settings.Value <- {Env.settings.Value with OPENAI_KEY = Some (Environment.GetEnvironmentVariable("OPENAI_API_KEY"))}
let gpt4Turbo = Env.settings.Value.OPENAI_MODELS.Value.CHAT.Head
let gpt32K    = Env.settings.Value.AZURE_OPENAI_MODELS.Value.CHAT |> List.find (fun x -> x.Contains("32k"))

let indexes,n = Indexes.fetch Env.settings.Value ["gc"] |> Env.runA
let indexNamed n = indexes |> List.find (function Azure vi -> vi.Name=n)

let MAX=120
let printText (xs:string) =
    let rec loop (x:string)  =
        if x.Length <= MAX then 
            printfn "%s" x
        else
            let i = 
                seq {for i in (MAX-1) .. -1 .. 0 do if xs.[i] = ' ' then yield i} 
                |> Seq.tryHead 
                |> Option.defaultValue MAX
            let y = x.Substring(0,i+1)
            let x = x.Substring(i+1).Trim()
            printfn "%s" y
            loop x
    use sr  = new StringReader(xs)
    let mutable line = sr.ReadLine()
    while line <> null do
        loop line
        line <- sr.ReadLine()

let query = @"What are the minimum distances between the E6160AC and B160 cabinets, for the standard site configuration?"

let chOpenAI = 
    let n,ch = Interaction.create(InteractionCreateType.CreateQA(Backend.OpenAI)) (Some query)
    ch
    |> Interaction.addIndex (indexNamed "ericsson-install")
    |> Interaction.addIndex (indexNamed "ericsson-gc-academy")
    |> Interaction.setParameters {ch.Parameters with ChatModel=gpt4Turbo}

let chAzure = 
    let n,ch = Interaction.create(InteractionCreateType.CreateQA(Backend.OpenAI)) (Some query)
    ch
    |> Interaction.addIndex (indexNamed "ericsson-install")
    |> Interaction.addIndex (indexNamed "ericsson-gc-academy")
    |> Interaction.setParameters {ch.Parameters with ChatModel=gpt4Turbo}

let ch = chOpenAI
let cogMems = QnA.chatPdfMemories Env.settings.Value ch

let refinedQuery = QnA.refineQueryTest Env.settings.Value ch Prompts.QnA.refineQuery2 |> Env.runA
printfn $"{refinedQuery}"

let searchResults = GenUtils.searchResults Env.settings.Value ch 10 refinedQuery cogMems

searchResults |> List.iter (fun x-> printfn "%A" (x.Relevance,x.Metadata.Description); printText x.Metadata.Text) 

let resp = QnA.answerQuestionTest Env.settings.Value ch searchResults |> Env.runT

printText resp





