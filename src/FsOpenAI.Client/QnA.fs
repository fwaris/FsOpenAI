namespace FsOpenAI.Client
open System
open FSharp.Control
open Microsoft.SemanticKernel
open Microsoft.SemanticKernel.Memory
open Azure.AI.OpenAI

module QnA =
    let history msgs =
        let hist = msgs |> List.map(fun m -> match m.Role with User _ -> $"[user]:{m.Message}" | Assistant -> $"[assistant]:{m.Message}")
        String.Join("\n\n",hist)        

    let joinText (rs:MemoryQueryResult seq) = String.Join("\n",rs |> Seq.map(fun x -> x.Metadata.Text))

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
                let chatHistory = msgs |> List.skipWhile (fun m-> m.IsUser) |> List.rev |> history
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

    let answerQuestion parms (ch:Interaction) docs dispatch = 
        task {
            try
                let question = ch.Messages |> List.rev |> List.find (fun m -> m.IsUser)
                let prompt = questionAnswerPrompt (DateTime.Now.ToShortDateString()) (joinText docs) question.Message
                let id,cs = Interactions.addNew(CreateChat ch.Parameters.Backend) (Some question.Message) []  //switch to chat model as GPT-4 does not support completion
                let p = {cs.[0].Parameters with ChatModel=ch.Parameters.ChatModel; CompletionsModel=ch.Parameters.CompletionsModel; EmbeddingsModel=ch.Parameters.EmbeddingsModel}
                let cs = Interactions.updateParms (id,p) cs
                let c = {cs.[0] with Id=ch.Id; InteractionType=Chat prompt}
                do! Completions.completeChat parms c dispatch        
            with ex -> 
                raise ex
        }


    ///semantic memory supporting chatpdf format
    let chatPdfMemory (parms:ServiceSettings) (ch:Interaction) : ISemanticTextMemory =
        let embModel = ch.Parameters.EmbeddingsModel
        let bag = match ch.InteractionType with QA bag -> bag | _ -> failwith "QA interaction expected"
        let indexName = match bag.Index with Some (Azure n) -> n.Name | _ -> failwith "No index selected"
        let idxClient = Indexes.searchClient parms
        let srchClient = idxClient.GetSearchClient(indexName)
        let openAIClient = Completions.getClient parms ch
        SemanticVectorSearch.CognitiveSearch(srchClient,openAIClient,embModel,bag.MaxDocs,"contentVector","content","sourcefile")

    let baseKernel (parms:ServiceSettings) (ch:Interaction) = 
        let chParms = ch.Parameters
        let chatModel = chParms.ChatModel
        let embModel = chParms.EmbeddingsModel
        let compModel = chParms.CompletionsModel
        let uri,key = Completions.getAzureEndpoint parms
        match ch.Parameters.Backend with 
        | AzureOpenAI _ ->
            KernelBuilder()                                        
                .WithAzureChatCompletionService(chatModel,uri,apiKey=key)
                .WithAzureTextEmbeddingGenerationService(embModel,uri,apiKey=key)
                .WithAzureChatCompletionService(chatModel,uri,apiKey=key)
                        
        | OpenAI _ ->
            let key = match parms.OPENAI_KEY with Some k -> k | None -> raise Utils.NoOpenAIKey
            KernelBuilder()
                .WithOpenAIChatCompletionService(chatModel,key)
                .WithOpenAITextCompletionService(compModel,key)
                .WithOpenAITextEmbeddingGenerationService(embModel,key)


    let runPlan (parms:ServiceSettings) (ch:Interaction) dispatch =
        async {  
            try
                let openAIClient = Completions.getClient parms ch
                let cogMem = chatPdfMemory parms ch 
                let! query = formulateQuery openAIClient ch.Parameters.CompletionsModel ch dispatch |> Async.AwaitTask
                dispatch (Srv_Ia_Notification (ch.Id,Some $"Searching with: {query}"))
                do! Async.Sleep 100
                let docs = cogMem.SearchAsync("",query) |> AsyncSeq.ofAsyncEnum |> AsyncSeq.toBlockingSeq |> Seq.toList
                dispatch (Srv_Ia_Notification(ch.Id,Some $"{docs.Length} query results found. Generating answer..."))
                do! answerQuestion parms ch docs dispatch |> Async.AwaitTask
            with ex -> dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }
