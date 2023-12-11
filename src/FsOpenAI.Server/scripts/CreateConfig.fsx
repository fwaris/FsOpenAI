#load "Env.fsx"
open FsOpenAI.Client
open System.IO
open System.Text.Json

let CONFIG_PATH = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"/../wwwroot/" + C.APP_CONFIG_PATH)

let saveConfig (config:AppConfig) (path:string) =
    let folder = Path.GetDirectoryName(path)
    if Directory.Exists folder |> not then Directory.CreateDirectory folder |> ignore    
    let json = JsonSerializer.Serialize(config,options=Utils.serOptions())
    System.IO.File.WriteAllText(path,json)

let accountingAppCfg = 
    {
        EnableOpenAI = false
        EnableDocQuery = true
        EnableVanillaChat = true
        Roles = ["OpenAIFinanceUsers"; "OpenAIFinanceAdmin"]
        RequireLogin = true
        PaletteDark = Some {AppPalette.Default with Primary=Some "#FF1E92"}
        PaletteLight = Some {AppPalette.Default with Primary=Some "#e20074"}
        LogoUrl = Some "https://mle.t-mobile.com/"
        IndexGroups = ["accounting"]
        DefaultMaxDocs = 10
        DefaultSystemMessage = """You are a helpful Accounting assistant.

You have knowledge of the accounting standards from FASB (https://fasb.org/standards). You understand the implications that the standards may have over many aspects of a business, e.g.: financial; economic; legal; contractual obligations; rights of the respective parties involved; risks of any kind; etc.

Thoroughly explore all implications when generating a response. Consider all aspects. If suitable, suggest remedies and alternate courses of actions that may be taken to mitigate any risks.

Your goal is to help the user work in a step by step way to resolve Accounting policy related questions. 

Be factual in your responses and cite the sources. Ask if you are not sure.
"""
    }

let pocAppCfg = 
    {
        EnableOpenAI = false
        EnableDocQuery = true
        EnableVanillaChat = true
        Roles = ["OpenAIDemoUsers"]
        RequireLogin = true
        PaletteDark = Some {AppPalette.Default with Primary=Some "#FF1E92"}
        PaletteLight = Some {AppPalette.Default with Primary=Some "#e20074"}
        LogoUrl = Some "https://mle.t-mobile.com/"
        IndexGroups = ["accounting"]
        DefaultMaxDocs = 10
        DefaultSystemMessage = """You are a helpful Accounting assistant.

You have knowledge of the accounting standards from FASB (https://fasb.org/standards). You understand the implications that the standards may have over many aspects of a business, e.g.: financial; economic; legal; contractual obligations; rights of the respective parties involved; risks of any kind; etc.

Thoroughly explore all implications when generating a response. Consider all aspects. If suitable, suggest remedies and alternate courses of actions that may be taken to mitigate any risks.

Your goal is to help the user work in a step by step way to resolve Accounting policy related questions. 

Be factual in your responses and cite the sources. Ask if you are not sure.
"""
    }

let gcAppCfg = 
    {
        EnableOpenAI = false
        EnableVanillaChat = true
        EnableDocQuery = false
        Roles = []
        RequireLogin = true
        PaletteDark = Some {AppPalette.Default with Primary=Some "#FF1E92"}
        PaletteLight = Some {AppPalette.Default with Primary=Some "#e20074"}
        LogoUrl = Some "https://mle.t-mobile.com/"
        IndexGroups = ["gc"]
        DefaultMaxDocs = 15
        DefaultSystemMessage = """You are a helpful telecom cell site construction assistant.

Thoroughly explore all implications when generating a response. Consider all aspects.

Be factual in your responses and cite the sources. Ask if you are not sure.
 """
    }


(*
saveConfig accountingAppCfg CONFIG_PATH

saveConfig pocAppCfg CONFIG_PATH

saveConfig gcAppCfg CONFIG_PATH
*)

