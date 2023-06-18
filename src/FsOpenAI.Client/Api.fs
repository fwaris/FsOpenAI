(*
#r "nuget: OpenAI"
*)
module Api 
open System
open OpenAI_API

type ApiParameters = 
    {
        OpenAIApiKey : string
        AzureApiKey : string
        AzureResourceGroup : string        
    }
    static member Default = 
        {
            OpenAIApiKey    = ""
            AzureApiKey = ""
            AzureResourceGroup = ""
        }

