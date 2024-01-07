namespace FsOpenAI.Client
open System
open System.Net.Http
open FSharp.Control
open FsOpenAI.Client
open FsOpenAI.Client.Interactions
open Microsoft.SemanticKernel

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

    let searchIndicated (answer:string) = (answer.Contains("bing.search", StringComparison.OrdinalIgnoreCase)) 

    let processWebChat (parms:ServiceSettings) (modelConfig:ModelsConfig) (ch:Interaction) dispatch =         
       async {
            try     
                let modelRefs = GenUtils.chatModels modelConfig ch.Parameters.Backend
                let kernel = (GenUtils.baseKernel parms modelRefs ch).Build()
                if parms.BING_ENDPOINT.IsNone then failwith "Bing endpoint not set"
                let bingKey = parms.BING_ENDPOINT.Value.API_KEY
                dispatch (Srv_Ia_Notification (ch.Id,"Querying model first"))                
                let question = Interaction.lastNonEmptyUserMessageText ch
                let args = GenUtils.kernelArgsDefault ["input",question]
                let! rslt = kernel.InvokePromptAsync(Prompts.WebSearch.answerQuestionOrDoSearch,arguments=args) |> Async.AwaitTask
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
                    let args = KernelArguments(GenUtils.promptSettings parms ch)
                    args.Add("externalInformation",information)
                    args.Add("input",question )
                    let args = GenUtils.kernelArgsFrom parms ch ["input",question; "externalInformation",information]
                    let promptSeq = kernel.InvokePromptStreamingAsync(Prompts.WebSearch.answerQuestion,arguments=args) 
                    do! Completions.streamCompleteFunction ch promptSeq dispatch
                else
                    dispatch (Srv_Ia_Notification(ch.Id,"Model was able to answer query by itself"))
                    dispatch (Srv_Ia_Delta(ch.Id,0,answer))
                dispatch(Srv_Ia_Done(ch.Id,None))
            with ex -> 
                dispatch(Srv_Ia_Done(ch.Id,Some ex.Message))
        }
