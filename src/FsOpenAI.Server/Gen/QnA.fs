namespace FsOpenAI.Client
open System
open FSharp.Control
open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.Memory
open FsOpenAI.Client.Interactions
open Azure.AI.OpenAI

module QnA =

    let history tknLimit msgs =
        let hist = 
            msgs
            |> List.filter (fun m -> Utils.notEmpty m.Message)
            |> List.map(fun m -> match m.Role with 
                                    | User _ -> $"[user]:{m.Message}" 
                                    | Assistant _ -> $"[assistant]:{m.Message}")
            |> List.map(fun t -> t,GenUtils.tokenSize t)
            |> List.scan (fun (_,acc) (t,c) -> t,acc + c ) ("",0.)
            |> List.skip 1
            |> List.takeWhile (fun (_,c) -> c < tknLimit)
            |> List.map fst            
        String.Join("\n\n",hist)        

    let trimMemories tknLimit (docs:MemoryQueryResult seq) =        
        docs
        |> Seq.sortByDescending(fun d->d.Relevance)
        |> Seq.map(fun d -> d, GenUtils.tokenSize d.Metadata.Text)
        |> Seq.scan (fun (_,acc) (t,c) -> Some t,acc + c ) (None,0.)
        |> Seq.skip 1
        |> Seq.takeWhile (fun (_,c) -> c < tknLimit)
        |> Seq.choose fst
        |> Seq.toList

    let combineSearchResults tknLimit (docs:MemoryQueryResult seq) = 
        let docs = trimMemories tknLimit docs 
        let docTexts = docs |> Seq.map(fun d -> d.Metadata.Text)
        String.Join("\r\r", docTexts)

    let formulateQueryPrompt chatHistory question = $"""
Below is a history of the conversation so far, and a new question asked by the user that needs to be answered by searching in a knowledge base.
Generate a search query based on the conversation and the new question.
Include in the search query any special terms mentioned in the question text so the right items in the knowlege base are included.
The search query should be optimized to find the answer to the question in the knowledge base.

Chat History:'''
{chatHistory}
'''

Question:'''
{question}
'''

Search query:
"""

    let formulateQuery (completionsClient:OpenAIClient) completionsModel (ch:Interaction) dispatch = 
        task {
            try
                let msgs = List.rev ch.Messages
                let tknBudget = GenUtils.safeTokenLimit completionsModel
                let tknsHist = tknBudget - 100.
                let chatHistory = msgs |> List.skipWhile (fun m-> m.IsUser) |> List.rev |> history tknsHist
                let question = msgs |> List.find (fun m -> m.IsUser)
                let prompt = formulateQueryPrompt chatHistory question
                let! resp = completionsClient.GetCompletionsAsync(CompletionsOptions(completionsModel, [prompt])) |> Async.AwaitTask
                return resp.Value.Choices.[0].Text
            with ex -> 
                return raise ex
        }



    let questionAnswerPrompt date documents question = $"""
SEARCH DOCUMENTS: '''
{documents}
'''
SEARCH DOCUMENTS is a collection of documents that match the queries in QUESTION.
Derive the best possible answers to the posed QUESTION from the content in SEARCH DOCUMENTS. 
BE BRIEF AND TO THE POINT, BUT WHEN SUPPLYING OPINION, IF YOU SEE THE NEED, YOU CAN BE LONGER.
WHEN ANSWERING QUESTIONS, GIVING YOUR OPINION OR YOUR RECOMMENDATIONS, BE CONTEXTUAL.
If you don't know, ask.
If you are not sure, ask.
Based on calculates from TODAY
TODAY is {date}

QUESTION: '''
{question}
'''

ANSWER:
"""
    let answerQuestion parms (ch:Interaction) docs dispatch = 
        task {
            try
                let tknBudget = GenUtils.safeTokenLimit ch.Parameters.ChatModel
                let tknsSearch = tknBudget - 500.
                let combinedSearch = combineSearchResults tknsSearch docs
                let question = Interaction.lastNonEmptyUserMessageText ch
                let prompt = questionAnswerPrompt combinedSearch (DateTime.Now) question
                let ch = Interaction.updateAndCloseLastUserMsg prompt ch
                do! Completions.streamCompleteChat parms ch dispatch
            with ex -> 
                raise ex
        }

    ///semantic memory supporting chatpdf format
    let chatPdfMemories (parms:ServiceSettings) (ch:Interaction) : ISemanticTextMemory list =
        let embModel = ch.Parameters.EmbeddingsModel
        let bag = match ch.InteractionType with QA bag -> bag | DocQA dbag -> dbag.QABag | _ -> failwith "QA interaction expected"
        if bag.Indexes.IsEmpty then failwith "No indexes selected"
        bag.Indexes
        |> List.map(fun idx -> 
            let indexName = match idx with Azure n -> n.Name 
            let idxClient = Indexes.searchServiceClient parms
            let srchClient = idxClient.GetSearchClient(indexName)
            let openAIClient = GenUtils.getClient parms ch
            SemanticVectorSearch.CognitiveSearch(true,srchClient,openAIClient,embModel,["contentVector"],"content","sourcefile","title"))

    let refineQuery parms ch (k:Kernel) userMessage chatHistory = 
        async {
            let fn = k.CreateFunctionFromPrompt(Prompts.QnA.refineQuery)
            let args = KernelArguments(userMessage.Message)    
            args.["chatHistory"] <- chatHistory
            let! ctx' = fn.InvokeAsync(k,args) |> Async.AwaitTask
            return ctx'.GetValue<string>()
        }

    let runPlan (parms:ServiceSettings) (ch:Interaction) dispatch =
        async {  
            try
                let cogMems = chatPdfMemories parms ch   
                let k = (GenUtils.baseKernel parms ch).Build()               
                let maxDocs = Interaction.maxDocs 1 ch              
                let msgs = ch.Messages |> List.rev |> List.skipWhile (fun x-> not x.IsUser)
                let userMessage = List.head msgs
                let historyMessages = List.tail msgs
                let chatModel = ch.Parameters.ChatModel
                let tknBudget = GenUtils.safeTokenLimit chatModel                
                let chatHistory = history tknBudget historyMessages
                printfn "Chat History: %A" chatHistory
                let! query = refineQuery parms ch k userMessage chatHistory
                dispatch (Srv_Ia_Notification (ch.Id,$"Searching with: {query}"))
                let docs = 
                    cogMems
                    |> AsyncSeq.ofSeq
                    |> AsyncSeq.collect(fun cogMem -> cogMem.SearchAsync("",query,maxDocs) |> AsyncSeq.ofAsyncEnum)                        
                    |> AsyncSeq.toBlockingSeq
                    |> Seq.toList
                let docs = docs |> List.sortByDescending (fun x->x.Relevance) |> List.truncate maxDocs
                dispatch (Srv_Ia_Notification(ch.Id,$"{docs.Length} query results found. Generating answer..."))
                dispatch (Srv_Ia_SetDocs (ch.Id,docs |> List.map(fun d -> 
                    {
                        Text=d.Metadata.Text
                        Embedding= if d.Embedding.HasValue then d.Embedding.Value.ToArray() else [||] 
                        Ref=d.Metadata.ExternalSourceName
                        Title = d.Metadata.Description
                        })))
                do! Async.Sleep 100
                do! answerQuestion parms ch docs dispatch |> Async.AwaitTask
            with ex -> dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }

    let answerQuestionTest parms (ch:Interaction) docs  = 
        task {
            let tknBudget = GenUtils.safeTokenLimit ch.Parameters.ChatModel
            let tknsSearch = tknBudget - 500.
            let combinedSearch = combineSearchResults tknsSearch docs
            let question = Interaction.lastNonEmptyUserMessageText ch
            let prompt = questionAnswerPrompt combinedSearch (DateTime.Now) question
            let ch = Interaction.updateAndCloseLastUserMsg prompt ch
            let! xs =  Completions.streamChat parms ch  
            let! acc = xs |> AsyncSeq.fold (fun acc (i,x) -> x::acc) [] 
            return String.Join("", List.rev acc)
        }

    let refineQueryTest (k:Kernel) userMessage chatHistory = 
        async {
            let fn = k.CreateFunctionFromPrompt(Prompts.QnA.refineQuery)
            let args = KernelArguments(userMessage.Message)    
            args.["chatHistory"] <- chatHistory
            let! ctx' = fn.InvokeAsync(k,args) |> Async.AwaitTask
            return ctx'.GetValue<string>()
        }
    