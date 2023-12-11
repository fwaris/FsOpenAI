namespace FsOpenAI.Client
open System
open System.Net.Http
open FSharp.Control
open FsOpenAI.Client
open FsOpenAI.Client.Interactions
open Azure.AI.OpenAI
open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.Connectors.AI.OpenAI
open Microsoft.SemanticKernel.TemplateEngine

module bingApi =
    let private searchQuery s = $"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(s)}&safeSearch=Strict&responseFilter=webPages&count=20"

    type WebPage = 
        {
            name    : string
            url     : string
            snippet : string
        }
    
    type WebPages = {
        value : WebPage list
    }

    type Resp = 
        {
            webPages : WebPages option
        }

    type HttpC(key:string) as this =
        inherit HttpClient()
        do
            this.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key",key) 
                  
    let search key s =
        async{
            use c = new HttpC(key)
            let uri = searchQuery s
            let! resp = c.GetStringAsync(uri) |> Async.AwaitTask
            try
                let wresp = Text.Json.JsonSerializer.Deserialize<Resp>(resp)
                return wresp
            with ex -> 
                return {webPages=None}
        }
    

module WebCompletion =
    let SemanticFunction = """
Answer questions only when you know the facts or the information is provided.
When you don't have sufficient information you reply with a list of commands to find the information needed.
When answering multiple questions, use a bullet point list.
Note: make sure single and double quotes are escaped using a backslash char.

[COMMANDS AVAILABLE]
- bing.search

[INFORMATION PROVIDED]
{{ $externalInformation }}

[EXAMPLE 1]
Question: what's the biggest lake in Italy?
Answer: Lake Garda, also known as Lago di Garda.

[EXAMPLE 2]
Question: what's the biggest lake in Italy? What's the smallest positive number?
Answer:
* Lake Garda, also known as Lago di Garda.
* The smallest positive number is 1.

[EXAMPLE 3]
Question: what's Ferrari stock price? Who is the current number one female tennis player in the world?
Answer:
{{ '{{' }} bing.search ""what\\'s Ferrari stock price?"" {{ '}}' }}.
{{ '{{' }} bing.search ""Who is the current number one female tennis player in the world?"" {{ '}}' }}.

[END OF EXAMPLES]

[TASK]
Question: {{ $input }}.
Answer: "
    """

    let SemanticFunctionNoCommand = """
Answer questions only when you know the facts or the information is provided.
When answering multiple questions, use a bullet point list.

[INFORMATION PROVIDED]
{{ $externalInformation }}

[EXAMPLE 1]
Question: what's the biggest lake in Italy?
Answer: Lake Garda, also known as Lago di Garda.

[EXAMPLE 2]
Question: what's the biggest lake in Italy? What's the smallest positive number?
Answer:
* Lake Garda, also known as Lago di Garda.
* The smallest positive number is 1.

[END OF EXAMPLES]

[TASK]
Question: {{ $input }}.
Answer: "
    """

    let searchIndicated (answer:string) = (answer.Contains("bing.search", StringComparison.OrdinalIgnoreCase)) 

    let processWebChat (parms:ServiceSettings) (ch:Interaction) dispatch =         
       async {
            try     
                let kernel = (GenUtils.baseKernel parms ch).Build()
                if parms.BING_ENDPOINT.IsNone then failwith "Bing endpoint not set"
                let bingKey = parms.BING_ENDPOINT.Value.API_KEY
                let oracle = kernel.CreateFunctionFromPrompt(SemanticFunction,new OpenAIPromptExecutionSettings(MaxTokens = 150, Temperature = 0, TopP = 1) )
                let question = Interaction.lastNonEmptyUserMessageText ch
                dispatch (Srv_Ia_Notification (ch.Id,"Querying model first"))                
                let! rslt = oracle.InvokeAsync(kernel, KernelArguments(question)) |> Async.AwaitTask                
                let answer = rslt.GetValue<string>()

                if searchIndicated answer then 
                    let webQ = 
                        try
                            let ans = (answer.Replace("\"\"","\""))
                            let vs = TemplateParser.extractVars ans
                            vs 
                            |> List.tryPick (function TemplateParser.FuncBlock ("bing.search",Some v) -> Some v | _ -> None)
                            |> Option.defaultValue question
                        with ex -> 
                            dispatch (Srv_Ia_Notification (ch.Id,"Model did not respond with a usable search query for Bing. Using user question instead for search"))
                            question

                    dispatch (Srv_Ia_Notification (ch.Id,"Model instructed to invoke websearch search. Searching..."))                    

                    let! webRslt = bingApi.search bingKey webQ 
                    
                    let information = 
                        match webRslt.webPages with 
                        | Some v when v.value.IsEmpty |> not ->
                            dispatch (Srv_Ia_Notification (ch.Id,"Got web search results. Querying model again with web data included"))
                            let docs = v.value |> List.map(fun w -> {Text=w.snippet; Embedding=[||]; Ref=w.url; Title=w.name})
                            dispatch (Srv_Ia_SetDocs(ch.Id,docs))
                            String.Join('\n', v.value |> List.map(fun w -> w.snippet))
                        | _ ->                        
                            dispatch (Srv_Ia_Notification (ch.Id,"Web search did not yield results. Continuing without web results"))
                            ""
                    let args = KernelArguments(question)
                    args.["externalInformation"] <- information
                    let! promptRslt = kernel.InvokePromptAsync(SemanticFunctionNoCommand,args) |> Async.AwaitTask
                    let prompt = promptRslt.GetValue<string>()
                    let ch = Interaction.updateAndCloseLastUserMsg prompt ch
                    do! Completions.streamCompleteChat parms ch dispatch
                else
                    dispatch (Srv_Ia_Notification(ch.Id,"Model was able to answer query by itself"))
                    dispatch (Srv_Ia_Delta(ch.Id,0,answer))
                dispatch(Srv_Ia_Done(ch.Id,None))
            with ex -> 
                dispatch(Srv_Ia_Done(ch.Id,Some ex.Message))
        }
