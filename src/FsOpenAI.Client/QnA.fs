namespace FsOpenAI.Client
open System
open FSharp.Control
open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.Memory
open FsOpenAI.Client.Interactions
open Microsoft.SemanticKernel.Connectors.AI.OpenAI.Tokenizers
open Microsoft.SemanticKernel.SemanticFunctions
open Azure.AI.OpenAI

module QnA =
    let tokenSize (s:string) = GPT3Tokenizer.Encode(s).Count

    let history buffer model msgs =
        let maxTkns = (Utils.safeTokenLimit model) - buffer |> max 0
        let hist = 
            msgs
            |> List.filter (fun m -> Utils.notEmpty m.Message)
            |> List.map(fun m -> match m.Role with 
                                    | User _ -> $"[user]:{m.Message}" 
                                    | Assistant -> $"[assistant]:{m.Message}")
            |> List.map(fun t -> t,GPT3Tokenizer.Encode(t).Count)
            |> List.scan (fun (_,acc) (t,c) -> t,acc + c ) ("",0)
            |> List.skip 1
            |> List.takeWhile (fun (_,c) -> c < maxTkns)
            |> List.map fst            
        String.Join("\n\n",hist)        

    let combineSearchResults buffer model (docs:MemoryQueryResult seq) =
        let maxTkns = (Utils.safeTokenLimit model) - buffer |> max 0
        let docs = 
            docs
            |> Seq.sortByDescending(fun d->d.Relevance)
            |> Seq.map(fun d -> d.Metadata.Text,GPT3Tokenizer.Encode(d.Metadata.Text).Count)
            |> Seq.scan (fun (_,acc) (t,c) -> t,acc + c ) ("",0)
            |> Seq.skip 1
            |> Seq.takeWhile (fun (_,c) -> c < maxTkns)
            |> Seq.map fst
            |> Seq.toList
        String.Join("\n\n",docs)

    let formulateQueryPrompt chatHistory question = $"""
Below is a history of the conversation so far, and a new question asked by the user that needs to be answered by searching in a knowledge base.
Generate a search query based on the conversation and the new question.
The search query should be optimized to find the answer to the question in the knowledge base.

Chat History:
{chatHistory}

Question:
{question}

Search query:
"""

    let formulateQuery (completionsClient:OpenAIClient) completionsModel (ch:Interaction) dispatch = 
        task {
            try
                let msgs = List.rev ch.Messages
                let chatHistory = msgs |> List.skipWhile (fun m-> m.IsUser) |> List.rev |> history 100 completionsModel
                let question = msgs |> List.find (fun m -> m.IsUser)
                let prompt = formulateQueryPrompt chatHistory question
                let! req = completionsClient.GetCompletionsStreamingAsync(completionsModel,CompletionsOptions([prompt]))
                let choices = req.Value.GetChoicesStreaming() |> AsyncSeq.ofAsyncEnum
                let texts = choices |> AsyncSeq.collect(fun c -> c.GetTextStreaming() |> AsyncSeq.ofAsyncEnum)
                let mutable resp = ""
                do! texts |> AsyncSeq.iter(fun t -> resp <- resp + t)
                return resp

//                 let xs =
//                     req.Value.GetChoicesStreaming() 
//                     |> AsyncSeq.ofAsyncEnum
//                     |> AsyncSeq.collect(fun x-> x.GetTextStreaming() |> AsyncSeq.ofAsyncEnum)
// //                    |> AsyncSeq.map(fun m-> dispatch(Srv_Ia_Notification(ch.Id,Some m));m)            
//                     |> AsyncSeq.toBlockingSeq
//                     |> Seq.toList
//                 return String.Join("",xs)
            with ex -> 
                return raise ex
        }



    let questionAnswerPrompt date documents question = $"""
SEARCH DOCUMENTS is a collection of documents that match the queries in QUESTION.
Derive the best possible answers to the posed QUESTION from the content in SEARCH DOCUMENTS. 
BE BRIEF AND TO THE POINT, BUT WHEN SUPPLYING OPINION, IF YOU SEE THE NEED, YOU CAN BE LONGER.
WHEN ANSWERING QUESTIONS, GIVING YOUR OPINION OR YOUR RECOMMENDATIONS, BE CONTEXTUAL.
If you don't know, ask.
If you are not sure, ask.
Based on calculates from TODAY

TODAY is {date}

[SEARCH DOCUMENTS]
{documents}

[QUESTION]
{question}

Answers:
"""

    let answerQuestion2 parms (ch:Interaction) docs dispatch = 
        task {
            try
                let question = ch.Messages |> List.rev |> List.find (fun m -> m.IsUser)
                let combinedSearch = combineSearchResults 500 ch.Parameters.ChatModel docs
                let prompt = questionAnswerPrompt (DateTime.Now.ToShortDateString()) combinedSearch question.Message
                let id,cs = Interactions.addNew(CreateChat ch.Parameters.Backend) (Some question.Message) []  //switch to chat model as GPT-4 does not support completion              
                let cs = Interactions.updateParms (id,ch.Parameters) cs
                let c = {cs.[0] with Id=ch.Id; InteractionType=Chat prompt}
                do! Completions.completeChat parms c dispatch        
            with ex -> 
                raise ex
        }

    let answerQuestion parms (ch:Interaction) docs dispatch = 
        task {
            try
                
                let combinedSearch = combineSearchResults 500 ch.Parameters.ChatModel docs
                let systemMessage = Prompts.qASystemPromptExclusive (Interaction.systemMessage ch) combinedSearch (DateTime.Now)
                let ch = Interaction.updateSystemMsg systemMessage ch
                do! Completions.completeChat parms ch dispatch
            with ex -> 
                raise ex
        }

    ///semantic memory supporting chatpdf format
    let chatPdfMemory (parms:ServiceSettings) (ch:Interaction) : ISemanticTextMemory =
        let embModel = ch.Parameters.EmbeddingsModel
        let bag = match ch.InteractionType with QA bag -> bag | _ -> failwith "QA interaction expected"
        let indexName = bag.Indexes |> List.tryHead |> Option.map(function Azure n -> n.Name) |> Option.defaultWith (fun _ -> failwith "No index selected")
        let idxClient = Indexes.searchClient parms
        let srchClient = idxClient.GetSearchClient(indexName)
        let openAIClient = Utils.getClient parms ch
        SemanticVectorSearch.CognitiveSearch(srchClient,openAIClient,embModel,"contentVector","content","sourcefile","title")

    ///semantic memory supporting chatpdf format
    let chatPdfMemories (parms:ServiceSettings) (ch:Interaction) : ISemanticTextMemory list =
        let embModel = ch.Parameters.EmbeddingsModel
        let bag = match ch.InteractionType with QA bag -> bag | _ -> failwith "QA interaction expected"
        if bag.Indexes.IsEmpty then failwith "No indexes selected"
        bag.Indexes
        |> List.map(fun idx -> 
            let indexName = match idx with Azure n -> n.Name |  _ -> failwith ""
            let idxClient = Indexes.searchClient parms
            let srchClient = idxClient.GetSearchClient(indexName)
            let openAIClient = Utils.getClient parms ch
            SemanticVectorSearch.CognitiveSearch(srchClient,openAIClient,embModel,"contentVector","content","sourcefile","title"))



    let runPlan2 (parms:ServiceSettings) (ch:Interaction) dispatch =
        async {  
            try
                let cogMem = chatPdfMemory parms ch                         //memory that supports chatpdf document format                
                let k = (Utils.baseKernel parms ch).WithMemory(cogMem).Build()               
                let maxDocs = Interaction.maxDocs 1 ch
                let completionsConfig = Utils.toCompletionsConfig ch.Parameters
                let msgs = ch.Messages |> List.rev |> List.skipWhile (fun x-> not x.IsUser)
                let userMessage = List.head msgs
                let historyMessages = List.tail msgs
                let chatModel = ch.Parameters.ChatModel
                let chatHistory = history (tokenSize Prompts.QnA.refineQuery) chatModel historyMessages
                printfn "Chat History: %A" chatHistory
                let fn = k.CreateSemanticFunction(Prompts.QnA.refineQuery,PromptTemplateConfig(Completion=completionsConfig))              
                let ctx = k.CreateNewContext()
                ctx.Variables.Set("INPUT",userMessage.Message)
                ctx.Variables.Set("chatHistory",chatHistory)      
                let! ctx' = fn.InvokeAsync(ctx) |> Async.AwaitTask
                let query = ctx'.Variables.Input
                //let openAIClient = Utils.getClient parms ch
                //let! query = formulateQuery openAIClient ch.Parameters.CompletionsModel ch dispatch |> Async.AwaitTask

                dispatch (Srv_Ia_Notification (ch.Id,$"Searching with: {query}"))
                let docs = k.Memory.SearchAsync("",query,maxDocs) |> AsyncSeq.ofAsyncEnum |> AsyncSeq.toBlockingSeq |> Seq.toList
                dispatch (Srv_Ia_Notification(ch.Id,$"{docs.Length} query results found. Generating answer..."))
                dispatch (Srv_Ia_SetDocs (ch.Id,docs |> List.map(fun d -> 
                    {
                        Text=d.Metadata.Text
                        Embedding=if d.Embedding.HasValue then d.Embedding.Value.Vector |> Seq.toArray else [||] 
                        Ref=d.Metadata.ExternalSourceName
                        Title = d.Metadata.Description
                        })))
                do! Async.Sleep 100
                do! answerQuestion2 parms ch docs dispatch |> Async.AwaitTask
            with ex -> dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }

    let runPlan (parms:ServiceSettings) (ch:Interaction) dispatch =
        async {  
            try
                let cogMems = chatPdfMemories parms ch   
                let k = (Utils.baseKernel parms ch).Build()               
                let maxDocs = Interaction.maxDocs 1 ch
                let completionsConfig = Utils.toCompletionsConfig ch.Parameters
                let msgs = ch.Messages |> List.rev |> List.skipWhile (fun x-> not x.IsUser)
                let userMessage = List.head msgs
                let historyMessages = List.tail msgs
                let chatModel = ch.Parameters.ChatModel
                let chatHistory = history (tokenSize Prompts.QnA.refineQuery) chatModel historyMessages
                printfn "Chat History: %A" chatHistory
                let fn = k.CreateSemanticFunction(Prompts.QnA.refineQuery,PromptTemplateConfig(Completion=completionsConfig))              
                let ctx = k.CreateNewContext()
                ctx.Variables.Set("INPUT",userMessage.Message)
                ctx.Variables.Set("chatHistory",chatHistory)      
                let! ctx' = fn.InvokeAsync(ctx) |> Async.AwaitTask
                let query = ctx'.Variables.Input
                dispatch (Srv_Ia_Notification (ch.Id,$"Searching with: {query}"))
                let docs = 
                    cogMems
                    |> AsyncSeq.ofSeq
                    |> AsyncSeq.collect(fun cogMem -> 
                        let k = (Utils.baseKernel parms ch).WithMemory(cogMem).Build()
                        k.Memory.SearchAsync("",query,maxDocs) |> AsyncSeq.ofAsyncEnum
                    )
                    |> AsyncSeq.toBlockingSeq
                    |> Seq.toList
                let docs = docs |> List.sortBy (fun x->x.Relevance) |> List.truncate maxDocs
                dispatch (Srv_Ia_Notification(ch.Id,$"{docs.Length} query results found. Generating answer..."))
                dispatch (Srv_Ia_SetDocs (ch.Id,docs |> List.map(fun d -> 
                    {
                        Text=d.Metadata.Text
                        Embedding=if d.Embedding.HasValue then d.Embedding.Value.Vector |> Seq.toArray else [||] 
                        Ref=d.Metadata.ExternalSourceName
                        Title = d.Metadata.Description
                        })))
                do! Async.Sleep 100
                do! answerQuestion parms ch docs dispatch |> Async.AwaitTask
            with ex -> dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }
