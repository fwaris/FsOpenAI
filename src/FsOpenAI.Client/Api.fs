module Api 
open System
open Azure.AI.OpenAI
open Azure

let endpoint rg = $"https://{rg}.openai.azure.com"
//let model = "davinci003"
let model = "gpt-35-turbo"

let newClient (k1,k2,rg) = 
    let key = Convert.FromBase64String k1
    let apiKey = SimpleCrypt.decr key k2
    let ep = new Uri(endpoint rg)
    let creds = AzureKeyCredential(apiKey)
    OpenAIClient(ep,creds)


let getCompletions (keys,chatMessages) = 
    let c1 = 
        ChatCompletionsOptions(
            MaxTokens = 1000,
            Temperature = 1.0f,
            FrequencyPenalty = 0.0f,
            PresencePenalty = 0.0f        
    )
    chatMessages |> List.iter (c1.Messages.Add)
    let c = newClient keys
    task {
        let! resp = c.GetChatCompletionsAsync(model,c1)    
        if resp.HasValue then 
            let topMsg = (resp.Value.Choices |> Seq.head).Message
            return chatMessages @ [topMsg]
        else
            return failwith $"Error calling api: {resp.GetRawResponse().ReasonPhrase}"
    }
