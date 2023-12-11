#r "nuget: Azure.AI.OpenAI, *-*"
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Plotly.NET"

open System
open System.IO
open Azure.AI.OpenAI
open Plotly.NET
open MathNet.Numerics.Statistics
open MathNet.Numerics.Distributions

let openAIKey           = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
let azureOpenAIKey      = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
let azureResourceGroup  = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_RG")
let azureEndpoint       = $"https://{azureResourceGroup}.openai.azure.com"

let openAIClient() = new OpenAIClient(openAIKey)
let azureClient() = new OpenAIClient(Uri(azureEndpoint),Azure.AzureKeyCredential(azureOpenAIKey))

let createChat (systemMessage:string) userQuestion =
    [
        if systemMessage.Trim().Length > 0 then 
            yield ChatMessage(ChatRole.System, systemMessage)
        yield ChatMessage(ChatRole.User,userQuestion)
    ]

let shorten (s:string) = if s.Length > 10 then s.Substring(0,20) + "..." else s

let runChat (client:OpenAIClient) model (systemMessage,userQuestion) = 
    let opts = ChatCompletionsOptions(model,  createChat systemMessage userQuestion)
    opts.Temperature <- 0.0f
    opts.FrequencyPenalty <- 0.0f
    opts.MaxTokens <- 3000
    opts.PresencePenalty <- 0.0f
    let resp = client.GetChatCompletions(opts)
    let msg = resp.Value.Choices.[0].Message.Content
    let len = float msg.Length
    printfn $"   {shorten systemMessage} | {shorten userQuestion} | {len}"
    len

let splitOn delim (xs:string seq) = 
    let rec loop acc ys xs =
       match xs with 
       | [] -> (List.rev ys)::acc |> List.rev
       | x::rest when x = delim -> loop (List.rev ys::acc) [] rest
       | x::rest -> loop acc (x::ys) rest
    loop [] [] (Seq.toList xs) 
    |> List.map(fun xs -> xs |> List.filter (fun x -> x.Trim().Length > 0))
    |> List.filter(List.isEmpty>>not)

let concat (xs:string seq) = String.Join(Environment.NewLine,xs).Trim()

let samples = 
    __SOURCE_DIRECTORY__ + @"\TestChats.txt" 
    |> File.ReadLines
    |> splitOn "!!"
    |> List.map(fun ys -> let ch = splitOn "++" ys in concat ch.[0], concat ch.[1])

let runSamples model client = samples |> List.map (runChat client model)

let openAIModels = ["gpt-4-1106-preview"] //; "gpt-4";"gpt-4-0314"] //; "gpt-4-0613";  ]
let azureModels = ["gpt-4"]

//runs 

let openAIRuns = 
    openAIModels 
    |> List.map(fun m -> m, [1..3] |> List.collect (fun i -> printfn $"OpenAI run: {m} {i}";  openAIClient() |> runSamples m)) 
    |> Map.ofList


let azureRuns =
    azureModels
    |> List.map(fun m -> m, [1..3] |> List.collect (fun i -> printfn $"Azure run: {m} {i}";  azureClient() |> runSamples m)) 
    |> Map.ofList

let tStatistic s1 s2 = 
    let s1 : float[] = Seq.toArray s1
    let s2 : float[] = Seq.toArray s2
    let mu1 = ArrayStatistics.Mean(s1)
    let sd1 = ArrayStatistics.Variance(s1)
    let mu2 = ArrayStatistics.Mean(s2)
    let sd2 = ArrayStatistics.Variance(s2)
    let samples = min s1.Length s2.Length |> float
    let t = (mu1 - mu2) / sqrt( (sd1/float s1.Length) + (sd2/float s2.Length ))
    abs t 

let tProb t df = 
    let std = StudentT(0.0,1.0,df)
    let cdf = std.CumulativeDistribution(t)
    2.0 * (1.0 - cdf)
    

let sample1 = openAIRuns.["gpt-4"]
let sample1b = openAIRuns.["gpt-4-0314"]
let sample2 = azureRuns.["gpt-4"]

let t0314 = tStatistic sample2 sample1b
let tProb0314 = tProb t0314 29.0

let tbase = tStatistic sample2 sample1
let tbaseProb = tProb tbase 29.0


[
    sample1b |> Chart.Violin |> Chart.withTraceInfo "OpenAI gpt-4-0314"
    sample2 |> Chart.Violin |> Chart.withTraceInfo "Azure gpt-4"
] 
|> Chart.combine |> Chart.withTitle "Distribution of the length of responses generated from 30 runs" |> Chart.show

(*
OpenAI run: gpt-4 1
   Your are a helpful A... | What is the meaning ... | 742
   You are a helpful Ac... | What is a substantiv... | 1386
   You are a helpful Ac... | What is a substantiv... | 2210
   You are a helpful Ac... | What is a substantiv... | 1805
   You are a helpful AI... | Describe the ASC 680... | 1989
   You are a helpful AI... | Under which Accounti... | 1372
   You are a helpful AI... | Does the amount in a... | 1387
   You are a helpful Ac... | Describe the process... | 2221
   You are a helpful Ac... | Describe the treatme... | 2421
   You are a helpful Ac... | Describe the treatme... | 2280
OpenAI run: gpt-4 2
   Your are a helpful A... | What is the meaning ... | 688
   You are a helpful Ac... | What is a substantiv... | 1619
   You are a helpful Ac... | What is a substantiv... | 2091
   You are a helpful Ac... | What is a substantiv... | 1778

   You are a helpful AI... | Describe the ASC 680... | 2098
   You are a helpful AI... | Under which Accounti... | 1072
   You are a helpful AI... | Does the amount in a... | 1176
   You are a helpful Ac... | Describe the process... | 3086
   You are a helpful Ac... | Describe the treatme... | 2761
   You are a helpful Ac... | Describe the treatme... | 2217
OpenAI run: gpt-4 3
   Your are a helpful A... | What is the meaning ... | 862
   You are a helpful Ac... | What is a substantiv... | 1399
   You are a helpful Ac... | What is a substantiv... | 2216
   You are a helpful Ac... | What is a substantiv... | 1715
   You are a helpful AI... | Describe the ASC 680... | 1875
   You are a helpful AI... | Under which Accounti... | 1394
   You are a helpful AI... | Does the amount in a... | 1257
   You are a helpful Ac... | Describe the process... | 3476
   You are a helpful Ac... | Describe the treatme... | 2643
   You are a helpful Ac... | Describe the treatme... | 2597
OpenAI run: gpt-4-0314 1
   Your are a helpful A... | What is the meaning ... | 498
   You are a helpful Ac... | What is a substantiv... | 2047
   You are a helpful Ac... | What is a substantiv... | 3215
   You are a helpful Ac... | What is a substantiv... | 3056
   You are a helpful AI... | Describe the ASC 680... | 2907
   You are a helpful AI... | Under which Accounti... | 1866
   You are a helpful AI... | Does the amount in a... | 1597
   You are a helpful Ac... | Describe the process... | 3792
   You are a helpful Ac... | Describe the treatme... | 2949
   You are a helpful Ac... | Describe the treatme... | 3061
OpenAI run: gpt-4-0314 2
   Your are a helpful A... | What is the meaning ... | 479
   You are a helpful Ac... | What is a substantiv... | 1833
   You are a helpful Ac... | What is a substantiv... | 2887
   You are a helpful Ac... | What is a substantiv... | 2238
   You are a helpful AI... | Describe the ASC 680... | 3755
   You are a helpful AI... | Under which Accounti... | 1904
   You are a helpful AI... | Does the amount in a... | 1594
   You are a helpful Ac... | Describe the process... | 3475
   You are a helpful Ac... | Describe the treatme... | 3165
   You are a helpful Ac... | Describe the treatme... | 3966
OpenAI run: gpt-4-0314 3
   Your are a helpful A... | What is the meaning ... | 520
   You are a helpful Ac... | What is a substantiv... | 2245
   You are a helpful Ac... | What is a substantiv... | 3300
   You are a helpful Ac... | What is a substantiv... | 2554
   You are a helpful AI... | Describe the ASC 680... | 3356
   You are a helpful AI... | Under which Accounti... | 1846
   You are a helpful AI... | Does the amount in a... | 1624
   You are a helpful Ac... | Describe the process... | 3421
   You are a helpful Ac... | Describe the treatme... | 2965
   You are a helpful Ac... | Describe the treatme... | 3590

OpenAI run: gpt-4-1106-preview 1
   Your are a helpful A... | What is the meaning ... | 1996
   You are a helpful Ac... | What is a substantiv... | 2932
   You are a helpful Ac... | What is a substantiv... | 3718
   You are a helpful Ac... | What is a substantiv... | 2988
   You are a helpful AI... | Describe the ASC 680... | 1114
   You are a helpful AI... | Under which Accounti... | 3078
   You are a helpful AI... | Does the amount in a... | 3063
   You are a helpful Ac... | Describe the process... | 4341
   You are a helpful Ac... | Describe the treatme... | 3537
   You are a helpful Ac... | Describe the treatme... | 3942
OpenAI run: gpt-4-1106-preview 2
   Your are a helpful A... | What is the meaning ... | 2298
   You are a helpful Ac... | What is a substantiv... | 2760
   You are a helpful Ac... | What is a substantiv... | 3512
   You are a helpful Ac... | What is a substantiv... | 3070
   You are a helpful AI... | Describe the ASC 680... | 920
   You are a helpful AI... | Under which Accounti... | 2782
   You are a helpful AI... | Does the amount in a... | 3286
   You are a helpful Ac... | Describe the process... | 4372
   You are a helpful Ac... | Describe the treatme... | 3902
   You are a helpful Ac... | Describe the treatme... | 3653
OpenAI run: gpt-4-1106-preview 3
   Your are a helpful A... | What is the meaning ... | 2251
   You are a helpful Ac... | What is a substantiv... | 2822
   You are a helpful Ac... | What is a substantiv... | 3291
   You are a helpful Ac... | What is a substantiv... | 3328
   You are a helpful AI... | Describe the ASC 680... | 1068
   You are a helpful AI... | Under which Accounti... | 2396
   You are a helpful AI... | Does the amount in a... | 3343
   You are a helpful Ac... | Describe the process... | 4385
   You are a helpful Ac... | Describe the treatme... | 3495
   You are a helpful Ac... | Describe the treatme... | 3884

Azure run: gpt-4 1
   Your are a helpful A... | What is the meaning ... | 685
   You are a helpful Ac... | What is a substantiv... | 1868
   You are a helpful Ac... | What is a substantiv... | 2456
   You are a helpful Ac... | What is a substantiv... | 1752
   You are a helpful AI... | Describe the ASC 680... | 1838
   You are a helpful AI... | Under which Accounti... | 879
   You are a helpful AI... | Does the amount in a... | 1173
   You are a helpful Ac... | Describe the process... | 3084
   You are a helpful Ac... | Describe the treatme... | 2766
   You are a helpful Ac... | Describe the treatme... | 2648
Azure run: gpt-4 2
   Your are a helpful A... | What is the meaning ... | 683
   You are a helpful Ac... | What is a substantiv... | 1143
   You are a helpful Ac... | What is a substantiv... | 2345
   You are a helpful Ac... | What is a substantiv... | 1737
   You are a helpful AI... | Describe the ASC 680... | 2089
   You are a helpful AI... | Under which Accounti... | 984
   You are a helpful AI... | Does the amount in a... | 1289
   You are a helpful Ac... | Describe the process... | 2844
   You are a helpful Ac... | Describe the treatme... | 2350
   You are a helpful Ac... | Describe the treatme... | 2355
Azure run: gpt-4 3
   Your are a helpful A... | What is the meaning ... | 705
   You are a helpful Ac... | What is a substantiv... | 1760
   You are a helpful Ac... | What is a substantiv... | 2396
   You are a helpful Ac... | What is a substantiv... | 2089
   You are a helpful AI... | Describe the ASC 680... | 1853
   You are a helpful AI... | Under which Accounti... | 1394
   You are a helpful AI... | Does the amount in a... | 1524
   You are a helpful Ac... | Describe the process... | 2566
   You are a helpful Ac... | Describe the treatme... | 2772
   You are a helpful Ac... | Describe the treatme... | 2194

   //t-tests
    val t0314: float = 2.929657828
    val tProb0314: float = 0.006549099047
    val tbase: float = 0.09180904661
    val tbaseProb: float = 0.9274810276

*)
let data1 = """
OpenAI run: gpt-4 1
   Your are a helpful A... | What is the meaning ... | 742
   You are a helpful Ac... | What is a substantiv... | 1386
   You are a helpful Ac... | What is a substantiv... | 2210
   You are a helpful Ac... | What is a substantiv... | 1805
   You are a helpful AI... | Describe the ASC 680... | 1989
   You are a helpful AI... | Under which Accounti... | 1372
   You are a helpful AI... | Does the amount in a... | 1387
   You are a helpful Ac... | Describe the process... | 2221
   You are a helpful Ac... | Describe the treatme... | 2421
   You are a helpful Ac... | Describe the treatme... | 2280
OpenAI run: gpt-4 2
   Your are a helpful A... | What is the meaning ... | 688
   You are a helpful Ac... | What is a substantiv... | 1619
   You are a helpful Ac... | What is a substantiv... | 2091
   You are a helpful Ac... | What is a substantiv... | 1778

   You are a helpful AI... | Describe the ASC 680... | 2098
   You are a helpful AI... | Under which Accounti... | 1072
   You are a helpful AI... | Does the amount in a... | 1176
   You are a helpful Ac... | Describe the process... | 3086
   You are a helpful Ac... | Describe the treatme... | 2761
   You are a helpful Ac... | Describe the treatme... | 2217
OpenAI run: gpt-4 3
   Your are a helpful A... | What is the meaning ... | 862
   You are a helpful Ac... | What is a substantiv... | 1399
   You are a helpful Ac... | What is a substantiv... | 2216
   You are a helpful Ac... | What is a substantiv... | 1715
   You are a helpful AI... | Describe the ASC 680... | 1875
   You are a helpful AI... | Under which Accounti... | 1394
   You are a helpful AI... | Does the amount in a... | 1257
   You are a helpful Ac... | Describe the process... | 3476
   You are a helpful Ac... | Describe the treatme... | 2643
   You are a helpful Ac... | Describe the treatme... | 2597
OpenAI run: gpt-4-0314 1
   Your are a helpful A... | What is the meaning ... | 498
   You are a helpful Ac... | What is a substantiv... | 2047
   You are a helpful Ac... | What is a substantiv... | 3215
   You are a helpful Ac... | What is a substantiv... | 3056
   You are a helpful AI... | Describe the ASC 680... | 2907
   You are a helpful AI... | Under which Accounti... | 1866
   You are a helpful AI... | Does the amount in a... | 1597
   You are a helpful Ac... | Describe the process... | 3792
   You are a helpful Ac... | Describe the treatme... | 2949
   You are a helpful Ac... | Describe the treatme... | 3061
OpenAI run: gpt-4-0314 2
   Your are a helpful A... | What is the meaning ... | 479
   You are a helpful Ac... | What is a substantiv... | 1833
   You are a helpful Ac... | What is a substantiv... | 2887
   You are a helpful Ac... | What is a substantiv... | 2238
   You are a helpful AI... | Describe the ASC 680... | 3755
   You are a helpful AI... | Under which Accounti... | 1904
   You are a helpful AI... | Does the amount in a... | 1594
   You are a helpful Ac... | Describe the process... | 3475
   You are a helpful Ac... | Describe the treatme... | 3165
   You are a helpful Ac... | Describe the treatme... | 3966
OpenAI run: gpt-4-0314 3
   Your are a helpful A... | What is the meaning ... | 520
   You are a helpful Ac... | What is a substantiv... | 2245
   You are a helpful Ac... | What is a substantiv... | 3300
   You are a helpful Ac... | What is a substantiv... | 2554
   You are a helpful AI... | Describe the ASC 680... | 3356
   You are a helpful AI... | Under which Accounti... | 1846
   You are a helpful AI... | Does the amount in a... | 1624
   You are a helpful Ac... | Describe the process... | 3421
   You are a helpful Ac... | Describe the treatme... | 2965
   You are a helpful Ac... | Describe the treatme... | 3590

OpenAI run: gpt-4-1106-preview 1
   Your are a helpful A... | What is the meaning ... | 1996
   You are a helpful Ac... | What is a substantiv... | 2932
   You are a helpful Ac... | What is a substantiv... | 3718
   You are a helpful Ac... | What is a substantiv... | 2988
   You are a helpful AI... | Describe the ASC 680... | 1114
   You are a helpful AI... | Under which Accounti... | 3078
   You are a helpful AI... | Does the amount in a... | 3063
   You are a helpful Ac... | Describe the process... | 4341
   You are a helpful Ac... | Describe the treatme... | 3537
   You are a helpful Ac... | Describe the treatme... | 3942
OpenAI run: gpt-4-1106-preview 2
   Your are a helpful A... | What is the meaning ... | 2298
   You are a helpful Ac... | What is a substantiv... | 2760
   You are a helpful Ac... | What is a substantiv... | 3512
   You are a helpful Ac... | What is a substantiv... | 3070
   You are a helpful AI... | Describe the ASC 680... | 920
   You are a helpful AI... | Under which Accounti... | 2782
   You are a helpful AI... | Does the amount in a... | 3286
   You are a helpful Ac... | Describe the process... | 4372
   You are a helpful Ac... | Describe the treatme... | 3902
   You are a helpful Ac... | Describe the treatme... | 3653
OpenAI run: gpt-4-1106-preview 3
   Your are a helpful A... | What is the meaning ... | 2251
   You are a helpful Ac... | What is a substantiv... | 2822
   You are a helpful Ac... | What is a substantiv... | 3291
   You are a helpful Ac... | What is a substantiv... | 3328
   You are a helpful AI... | Describe the ASC 680... | 1068
   You are a helpful AI... | Under which Accounti... | 2396
   You are a helpful AI... | Does the amount in a... | 3343
   You are a helpful Ac... | Describe the process... | 4385
   You are a helpful Ac... | Describe the treatme... | 3495
   You are a helpful Ac... | Describe the treatme... | 3884

Azure run: gpt-4 1
   Your are a helpful A... | What is the meaning ... | 685
   You are a helpful Ac... | What is a substantiv... | 1868
   You are a helpful Ac... | What is a substantiv... | 2456
   You are a helpful Ac... | What is a substantiv... | 1752
   You are a helpful AI... | Describe the ASC 680... | 1838
   You are a helpful AI... | Under which Accounti... | 879
   You are a helpful AI... | Does the amount in a... | 1173
   You are a helpful Ac... | Describe the process... | 3084
   You are a helpful Ac... | Describe the treatme... | 2766
   You are a helpful Ac... | Describe the treatme... | 2648
Azure run: gpt-4 2
   Your are a helpful A... | What is the meaning ... | 683
   You are a helpful Ac... | What is a substantiv... | 1143
   You are a helpful Ac... | What is a substantiv... | 2345
   You are a helpful Ac... | What is a substantiv... | 1737
   You are a helpful AI... | Describe the ASC 680... | 2089
   You are a helpful AI... | Under which Accounti... | 984
   You are a helpful AI... | Does the amount in a... | 1289
   You are a helpful Ac... | Describe the process... | 2844
   You are a helpful Ac... | Describe the treatme... | 2350
   You are a helpful Ac... | Describe the treatme... | 2355
Azure run: gpt-4 3
   Your are a helpful A... | What is the meaning ... | 705
   You are a helpful Ac... | What is a substantiv... | 1760
   You are a helpful Ac... | What is a substantiv... | 2396
   You are a helpful Ac... | What is a substantiv... | 2089
   You are a helpful AI... | Describe the ASC 680... | 1853
   You are a helpful AI... | Under which Accounti... | 1394
   You are a helpful AI... | Does the amount in a... | 1524
   You are a helpful Ac... | Describe the process... | 2566
   You are a helpful Ac... | Describe the treatme... | 2772
   You are a helpful Ac... | Describe the treatme... | 2194

"""
let readData() = 
        seq {
            use str = new StringReader(data1)
            let mutable line = null
            line <- str.ReadLine()
            while line <> null do
                yield line
                line <- str.ReadLine()
        }
let ls1 = readData() |> Seq.take 10 |> Seq.toList

let groupByModel lines = 
    let rec loop acc (topic:string option) ys (xs:string list) = 
        match xs with
        | [] when topic.IsNone             -> acc |> List.rev
        | []                               -> (topic.Value,List.rev ys)::acc |> List.rev
        | x::rest when x.Trim().Length = 0 -> loop acc topic ys rest
        | x::rest when x.[0] = ' '        -> loop acc topic (x::ys) rest
        | x::rest when topic.IsNone        -> loop acc (Some x) [] rest
        | x::rest                          -> loop ((topic.Value,List.rev ys)::acc) (Some x) [] rest
    loop [] None [] lines

let modelName (s:string) = s.Split(" ").[2]
let lengthVal (s:string) = s.Split("|") |> Array.item 2 |> float

let groups = 
    readData() 
    |> Seq.toList 
    |> groupByModel 
    |> List.map(fun (m,xs) -> modelName m,xs)
    |> List.groupBy fst
    |> List.map (fun (k,xs) -> k,xs |> List.collect snd )
    |> List.map (fun (k,xs) -> k,xs |> List.map lengthVal )

let s1 = groups |> List.find (fun (k,xs) -> k = "gpt-4-1106-preview") |> snd
let s2 = groups |> List.find (fun (k,xs) -> k = "gpt-4-0314")         |> snd

let tpreview = tStatistic s1 s2
let tprobPreview = tProb tpreview 29.0


groups |> List.map(fun (k,xs) -> xs |> Chart.Violin |> Chart.withTraceInfo k) |> Chart.combine |> Chart.show







