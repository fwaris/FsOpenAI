#load "Env.fsx"
open FsOpenAI.Client
open System.IO
open System.Text.Json

let saveConfig (config:AppConfig) (path:string) =
    let folder = Path.GetDirectoryName(path)
    if Directory.Exists folder |> not then Directory.CreateDirectory folder |> ignore    
    let json = JsonSerializer.Serialize(config,options=Utils.serOptions())
    System.IO.File.WriteAllText(path,json)

let pocAppCfg = 
    {
        EnableOpenAI = true
        EnableVanillaChat = true
        Roles = []
        RequireLogin = false
        PaletteDark = None //Some {AppPalette.Default with Primary=Some "#FF1E92"}
        PaletteLight = None // Some {AppPalette.Default with Primary=Some "#e20074"}
        LogoUrl = Some "https://github.com/fwaris/FsOpenAI"
        IndexGroups = []
    }

let fn = Path.GetFullPath(__SOURCE_DIRECTORY__ + @"/../wwwroot/" + C.APP_CONFIG_PATH)

(*
saveConfig pocAppCfg fn
*)





