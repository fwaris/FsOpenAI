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


let getCompletions (keys,chatMessages,options:ChatCompletionsOptions) = 
    chatMessages |> List.iter (options.Messages.Add)
    let c = newClient keys
    task {
        let! resp = c.GetChatCompletionsAsync(model,options)    
        if resp.HasValue then 
            let topMsg = (resp.Value.Choices |> Seq.head).Message
            return (chatMessages |> List.filter(fun x-> x.Role<>ChatRole.System)) @ [topMsg]
        else
            return failwith $"Error calling api: {resp.GetRawResponse().ReasonPhrase}"
    }
