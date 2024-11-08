namespace FsOpenAI.GenAI
open System
open System.Net.Http
open FSharp.Control
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
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

    let askModel parms invCtx ch dispatch = 
        async {
            dispatch (Srv_Ia_Notification (ch.Id,"Querying model first"))                
            let question = Interaction.lastNonEmptyUserMessageText ch
            let args = GenUtils.kernelArgsDefault ["input",question] 
            let! prompt = GenUtils.renderPrompt Prompts.WebSearch.answerQuestionOrDoSearch args |> Async.AwaitTask
            let ch = Interaction.setUserMessage prompt ch
            let! rslt = Completions.completeChat parms invCtx ch dispatch None None
            return (rslt.Content,question)
        }

    let processWebChat (parms:ServiceSettings) (invCtx:InvocationContext) (ch:Interaction) dispatch =         
       async {
            try     
                if parms.BING_ENDPOINT.IsNone then failwith "Bing endpoint not set"
                let bingKey = parms.BING_ENDPOINT.Value.API_KEY
                let! answer,question = askModel parms invCtx ch dispatch
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
                    let args = GenUtils.kernelArgsFrom parms ch ["input",question; "externalInformation",information]
                    let! prompt = GenUtils.renderPrompt Prompts.WebSearch.answerQuestion args |> Async.AwaitTask
                    let ch = Interaction.setUserMessage prompt ch                   
                    do! Completions.checkStreamCompleteChat parms invCtx ch dispatch None true
                else
                    dispatch (Srv_Ia_Notification(ch.Id,"Model was able to answer query by itself"))

                    dispatch (Srv_Ia_Delta(ch.Id,0,answer))
                dispatch(Srv_Ia_Done(ch.Id,None))
            with ex -> 
                dispatch(Srv_Ia_Done(ch.Id,Some ex.Message))
        }
