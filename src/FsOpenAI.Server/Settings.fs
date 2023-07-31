namespace FsOpenAI.Server
open System
open System.IO
open FSharp.Control
open Azure.Identity;
open Azure.Security.KeyVault.Secrets
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration


module Env =
    module N =
        let SETTINGS_FILE = "Parms:Settings"                          //1st: look for file key in appSettings.json
        let FSOPENAI_AZURE_KEYVAULT = "FSOPENAI_AZURE_KEYVAULT"       //2nd: look for in key vault
        let KEY_VAULT_KEY = "genai-settings"

    type Config = 
        {
            SettingsFile : string
            KeyVault : string
        }
        with static member Default = {SettingsFile=""; KeyVault=""}

    let mutable private config = Config.Default
    let mutable private logger : ILogger<string> = Unchecked.defaultof<_>

    let init (cfg:IConfiguration, lggr:ILogger<string>) =
        logger <- lggr
        config <- 
            {
                SettingsFile = cfg.[N.SETTINGS_FILE]
                KeyVault = System.Environment.GetEnvironmentVariable(N.FSOPENAI_AZURE_KEYVAULT)
            }

    let logError str =
        if logger <> Unchecked.defaultof<_> then
            logger.LogError(str)

    let getFromKeyVault() =
        task {            
            let kvUri = $"https://{config.KeyVault}.vault.azure.net";
            let c = new DefaultAzureCredential()
            let client = new SecretClient(new Uri(kvUri), c);
            try             
                let! sec = client.GetSecretAsync(N.KEY_VAULT_KEY)                
                return sec.Value.Value |> Convert.FromBase64String |> System.Text.UTF8Encoding.Default.GetString
            with ex -> 
                let msg = $"unable to get secret {ex.Message}"
                logError msg
                return raise ex
        }

    let readSettingsFile (path:string) = 
        task {
            try
                return System.IO.File.ReadAllText path
            with ex ->
               let msg = $"error loading settings file '{config.SettingsFile}'"
               logError msg
               return raise ex
        }

    let getParameters() = 
        task {
            let! settingsText = 
                if File.Exists config.SettingsFile then 
                    readSettingsFile config.SettingsFile
                else
                    getFromKeyVault()
            let parms = System.Text.Json.JsonSerializer.Deserialize<FsOpenAI.Client.ServiceSettings>(settingsText)
            return parms
        }
