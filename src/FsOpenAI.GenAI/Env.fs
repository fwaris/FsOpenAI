namespace FsOpenAI.GenAI
open System
open System.IO
open FSharp.Control
open Azure.Identity;
open Azure.Security.KeyVault.Secrets
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration
open FsOpenAI.Shared
open System.Text.Json

type FsOpenAILog() = class end //type for log categorization

module Env =    
    type Config = 
        {
            SettingsFile : string
            SettingsFile_Env : string
            KeyVault : string
            KeyVaultKey : string 
            WwwRoot : string
        }
        with static member Default = 
                                {
                                    SettingsFile=""
                                    SettingsFile_Env=""
                                    KeyVault=""
                                    KeyVaultKey=""
                                    WwwRoot=""
                                }

    let mutable private config = Config.Default
    let mutable private logger : ILogger<FsOpenAILog> = Unchecked.defaultof<_>

    let wwwRootPath() = config.WwwRoot

    let logError str =
        if logger <> Unchecked.defaultof<_> then
            logger.LogError(str)

    let logInfo str =
        if logger <> Unchecked.defaultof<_> then
            logger.LogInformation(str)

    let logException (exn:Exception,str:string) =
        if logger <> Unchecked.defaultof<_> then
            logger.LogError(exn,str)

    let getFromKeyVault() =
        task {            
            let kvUri = $"https://{config.KeyVault}.vault.azure.net";
            let c = new DefaultAzureCredential()
            let client = new SecretClient(new Uri(kvUri), c);
            try             
                let! sec = client.GetSecretAsync(config.KeyVaultKey)                
                return sec.Value.Value |> Convert.FromBase64String |> System.Text.UTF8Encoding.Default.GetString
            with ex -> 
                let msg = $"unable to get secret {ex.Message}"
                logError msg
                return raise ex
        }

    let getLogger() = if logger <> Unchecked.defaultof<_> then None else Some logger

    let readSettingsFile (path:string) = 
        task {
            try
                return System.IO.File.ReadAllText path
            with ex ->
               let msg = $"error loading settings file '{config.SettingsFile}'"
               logError msg
               return raise ex
        }

    let defaultSettings() = 
        {
            LOG_CONN_STR = None
            AZURE_OPENAI_ENDPOINTS = []
            AZURE_SEARCH_ENDPOINTS = []
            EMBEDDING_ENDPOINTS = []
            BING_ENDPOINT = None
            OPENAI_KEY = None
        }

    let loadSettingsAsync() = 
        task {
            let! settingsText = 
                let path = 
                    if not(String.IsNullOrWhiteSpace config.SettingsFile) then
                        Environment.ExpandEnvironmentVariables(config.SettingsFile)
                    else
                        ""
                if File.Exists path then
                    logInfo $"reading settings from {path}"
                    readSettingsFile path
                elif not (String.IsNullOrWhiteSpace config.SettingsFile_Env) then
                    logInfo $"reading settings from env variable"
                    let bytes = Convert.FromBase64String config.SettingsFile_Env
                    task{ return System.Text.UTF8Encoding.Default.GetString bytes}
                else
                    getFromKeyVault()
            let parms = JsonSerializer.Deserialize<ServiceSettings>(settingsText,Utils.serOptions())
            return parms
        }        

    let tryLoadSettings() = loadSettingsAsync() |> Async.AwaitTask |> Async.RunSynchronously

    let loadSettings() = try tryLoadSettings() with ex -> logException (ex,"load settings"); defaultSettings()

    let loadConfig() =
        try
            let path = Path.GetFullPath(Path.Combine(wwwRootPath(),C.APP_CONFIG_PATH))
            let str = File.ReadAllText path
            let appConfig : AppConfig = JsonSerializer.Deserialize(str,Utils.serOptions())
            Some appConfig
        with ex ->
            printfn "%A" ex            
            None

    let appConfig = lazy(loadConfig())

    let init (cfg:IConfiguration, lggr:ILogger<FsOpenAILog>, wrootPath) =
        logger <- lggr
        config <- 
            {
                SettingsFile = cfg.[C.SETTINGS_FILE]
                SettingsFile_Env = cfg.[C.SETTINGS_FILE_ENV]
                KeyVault = System.Environment.GetEnvironmentVariable(C.FSOPENAI_AZURE_KEYVAULT)
                KeyVaultKey = System.Environment.GetEnvironmentVariable(C.FSOPENAI_AZURE_KEYVAULT_KEY)
                WwwRoot = wrootPath
            }

module Settings =
    let mutable _cachedSettings = lazy(Env.loadSettings()) //mutable to allow refresh even if app is running

    let redactEndpoints (xs:AzureOpenAIEndpoints list) =
        xs 
        |> List.map(fun c -> {c with API_KEY = "Redacted"})

    let redactEndpoint (ep:ApiEndpoint) = {ep with API_KEY="Redacted"}

    let redactSearchEndpoints (xs:ApiEndpoint list) = xs |> List.map redactEndpoint

    let redactKeys (sttngs:ServiceSettings) =
        {
            LOG_CONN_STR = None
            OPENAI_KEY = None
            AZURE_OPENAI_ENDPOINTS = redactEndpoints sttngs.AZURE_OPENAI_ENDPOINTS
            AZURE_SEARCH_ENDPOINTS = redactSearchEndpoints sttngs.AZURE_SEARCH_ENDPOINTS
            EMBEDDING_ENDPOINTS = redactEndpoints sttngs.EMBEDDING_ENDPOINTS
            BING_ENDPOINT = sttngs.BING_ENDPOINT |> Option.map redactEndpoint
        }
          
    let refreshSettings dispatch = 
        task {
            try
                let! sttngs = Env.loadSettingsAsync()
                _cachedSettings <- lazy(sttngs)
                let clSttngs = redactKeys sttngs
                dispatch (Srv_Parameters clSttngs)
            with ex ->
               Env.logException (ex,"refreshSettings")
               let msg = $"Using default settings (custom settings not configured)"
               dispatch (Srv_Parameters _cachedSettings.Value)
               dispatch (Srv_Info msg)
        }
    
    let getSettings() = _cachedSettings

    let updateKey sttngs = 
        match sttngs.OPENAI_KEY with 
        | None -> _cachedSettings.Value
        | _    -> {_cachedSettings.Value with OPENAI_KEY = sttngs.OPENAI_KEY} //use openai from client, if provided
