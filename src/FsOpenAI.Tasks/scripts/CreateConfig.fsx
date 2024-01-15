#load "Env.fsx"
open FsOpenAI.Client
open System.IO
open System.Text.Json

let CONFIG_PATH = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"../../../FsOpenAI.Server/wwwroot/" + C.APP_CONFIG_PATH)

let saveConfig (config:AppConfig) (path:string) =
    let folder = Path.GetDirectoryName(path)
    if Directory.Exists folder |> not then Directory.CreateDirectory folder |> ignore    
    let json = JsonSerializer.Serialize(config,options=Utils.serOptions())
    System.IO.File.WriteAllText(path,json)

let embedding = 
    [
        {Backend=AzureOpenAI; Model="text-embedding-ada-002"; TokenLimit=8192} //only azure is supported for embeddings        
        {Backend=AzureOpenAI_Basic; Model="text-embedding-ada-002"; TokenLimit=8192} //only azure is supported for embeddings        
    ]
let shortChat = 
    [
        {Backend=AzureOpenAI; Model="gpt-4"; TokenLimit=8000}; 
        {Backend=AzureOpenAI_Basic; Model="gpt-35-turbo"; TokenLimit=4000}; 
        {Backend=OpenAI; Model="gpt-4-1106-preview"; TokenLimit=127000}; 
    ]
let longChat = 
    [
        {Backend=AzureOpenAI; Model="gpt-4-32k"; TokenLimit=30000}
        {Backend=AzureOpenAI_Basic; Model="gpt-35-turbo-16k"; TokenLimit=15000}; 
        {Backend=OpenAI; Model="gpt-4-1106-preview"; TokenLimit=127000}
    ]
let completion = 
    [
        {Backend=AzureOpenAI; Model="text-davinci-003"; TokenLimit=4000}
        {Backend=OpenAI; Model="text-davinci-003"; TokenLimit=4000}
    ]
let modelsConfig = 
    {
        EmbeddingsModels = embedding
        ShortChatModels = shortChat
        LongChatModels = longChat
        CompletionModels = completion
    }

let pocAppCfg = 
    {
        EnabledBackends = [OpenAI; AzureOpenAI; AzureOpenAI_Basic]
        EnableDocQuery = true
        EnableVanillaChat = true
        Roles = []
        RequireLogin = false
        PaletteDark = None 
        PaletteLight = None
        AppName = None 
        PersonaText = None 
        LogoUrl = None
        MetaIndex = None
        IndexGroups = []
        DefaultMaxDocs = 10
        DefaultSystemMessage = "You are a helpful AI assistant."
        ModelsConfig = modelsConfig
    }

(*
saveConfig pocAppCfg CONFIG_PATH
*)

