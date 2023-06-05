#load "packages.fsx"
open Azure.AI.OpenAI

open System
let key = System.Environment.GetEnvironmentVariable("SC_KEY")   //generate a new key if requires using see SimpleCrypt.fs
let apikey = System.Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
let resourceGroup = System.Environment.GetEnvironmentVariable("AZURE_OPENAI_API_RG")
let encApiKey = SimpleCrypt.encr (Convert.FromBase64String key) apikey //protects from logging in the browser

let c = Api.newClient(key,encApiKey,resourceGroup)
let cmsg = ChatMessage(role=ChatRole.User,content="what is the best car")
let c1 = 
    ChatCompletionsOptions(
        MaxTokens = 1000,
        Temperature = 1.0f,
        FrequencyPenalty = 0.0f,
        PresencePenalty = 0.0f        
)
c1.Messages.Add(cmsg)

let rslt = c.GetChatCompletions(Api.model,c1)
rslt.Value.Choices.[0].Message
rslt.Value.Choices.Count


