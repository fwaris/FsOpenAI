#load "../../scripts/ScriptEnv.fsx"
open System.IO
open FsOpenAI.Shared
open FsOpenAI.GenAI
open ScriptEnv

//FsOpenAI is meant to configurable. This script configures the default (demo) settings for the app
//this script copies the AppConfig.json; appSettings.json; Samples; Templates; Branding images; etc.
//to appropriate locations (under server and client wwwroots) to make the
//app run in a particular configuration
//Make copies of this script (or the folder structure 'default')
//to create different configurations for deployment

//Optional name of the meta index. Meta index points to real indexes that contain doc collections
//If defined, the app will read the meta index first and list the indexes in the QnA chat mode
let metaIndexName = Some $"{C.DEFAULT_META_INDEX}"

//The keyvault key where the settings file will be stored
//The settings file is base64 encoded and stored in the keyvault under this key
//For Azure deployments, its preferrable to store the settings file in a keyvault
let keyVaultKey = "fsopenai"

//The Azure keyvault where the settings file will be stored with the key above
let keyVault = "your-keyvault-name"

//The location of the settings file for this config.
//It will be copied to (%USERPROFILE%|%HOME%)/.fsopenai/ServiceSettings.json so that it is 'in-effect'
//for local development.
let baseSettingsFile = @".fsopenai/poc/ServiceSettings.json"


//The root folder for client files. These will be copied to client wwwroot. Contains:
//- appSettings.json - this may contain Azure Entra ID config for authentication
//- app/imgs/{favicon.ico|logo.png|persona.png (opt.)} - branding images for the app
let clientFiles = "client"

//The root folder for server files. These will be copied to server wwwroot.
//Contains: appSettings.json with server side configuration
let serverFiles = "server"

(*
The root folder for templates and samples. These will be copied to server wwwroot/app/Templates.
The structure is as follows:

- <sub folder 1>/Samples.json
- <sub folder 1>/<SemanticKernel 'plugin' files> - templates for the chat

Notes: <sub folder 1> (there can be many) is the 'group' name.
Each group can have its own samples and templates.
*)
let templates = "templates"

//default system message for the chat
let defaultSysMessage = """You are a helpful AI assistant"""

//see config_default.fsx for more details
//this is the meta index that points to real indexes for this config
//the real index is expected to exist
let docDesc =
    [
        //index name, tag, description, isVirtual, parents
        "ml-docs", "ml-docs", "ML Document Collection",true,[] // parent index (virtual)
        "ml-math","ml-math","Mathematics for ML",false,["ml-docs"] //child index 1
        "pattern-recognition","pattern-recognition","Pattern Recognition",false,["ml-docs"] //child index 2
    ]

let docs =
    docDesc
    |> List.map(fun (n,tag,d,isV,parents) ->
        {MetaIndexEntry.Default with
            title=n
            description=d
            tag=tag
            groups=["default"] //should match group in app settings config below
            isVirtual=isV;
            parents=parents}
        )

//The main application configuration settings
//The content of this record will serialized to json and stored under server wwwroot/app/AppConfig.json
//The app reads this file at startup to configure itself
let acctAppCfg =
    {
        EnabledBackends = [OpenAI] // [AzureOpenAI; OpenAI] //list of 'backends' that the user may select from (can be expanded in the future)
        //EnabledChatModes = [M_Index,defaultSysMessage; M_Doc_Index, defaultSysMessage; M_Doc, defaultSysMessage; M_Plain, defaultSysMessage; M_CodeEval,"ignored"] //list of chat modes that may be enabled in the app
        EnabledChatModes = [M_Index,defaultSysMessage; M_Doc_Index, defaultSysMessage; M_Doc, defaultSysMessage; M_Plain, defaultSysMessage] //; M_CodeEval,"ignored"] //list of chat modes that may be enabled in the app
        DatabaseName = C.DFLT_COSMOSDB_NAME //name of the CosmosDB database
        DiagTableName = None //Some "log1" // CosmosDB container name where to store chat submission logs
        SessionTableName = None // Some "sessions" // Some "sessions" persist sessions to CosmosDB
        AppBarType = Some (AppB_Base "FsOpenAI Chat") //Header bar style and title text
        Roles = [] //if not empty app will only allow users that have the listed roles (from AD; case sensitive)
        RequireLogin = false //if true, requires AD login (via MSAL); needs valid appSettings.json (see above)
        AssistantIcon = None
        AssistantIconColor = None
        LogoUrl = Some "https:/github.com/fwaris/FsOpenAI" //url associated with app logo (shown in the header)
        AppName = Some "FsOpenAI Chat" //shows as tab text in the browser
        AppId = Some "default" //unique id for the app for logging purposes
        PersonaText = Some "FsOpenAI Chat"      //if set the persona image along with text (sub text, next) will be 'flashed' at startup.
        PersonaSubText = Some "Loading ..." //if set the persona image along with text (sub text, next) will be 'flashed' at startup.
        Disclaimer = None   //text show in the footer (e.g . "LLM answers may not be accurate and should be validated")
        IndexGroups = ["default"] //list of index groups that the app will show in the index dropdown
        DefaultMaxDocs = 10
        MetaIndex = metaIndexName
        //ModelsConfig = ScriptEnv.ModelDefs.modelsConfig //model and token limits for different backends
        ModelsConfig = ScriptEnv.ModelDefs.modelsConfigLogic //model and token limits for different backends
    }

//samples

let templatesPath = Path.GetFullPath(__SOURCE_DIRECTORY__ + @$"/{templates}")

let SamplesPath = templatesPath @@ "default" @@ "Samples.json"
let samples =
    [
        {
            SampleChatType = SM_IndexQnA "ml-docs"
            SampleMode = ExplorationMode.Factual
            MaxDocs     = 5
            SampleSysMsg  = defaultSysMessage
            SampleQuestion = "How to prevent overfitting?"
        }
        (*
        *)
        {
            SampleChatType = SM_CodeEval
            SampleMode = ExplorationMode.Factual
            MaxDocs     = 5
            SampleSysMsg  = "ignored"
            SampleQuestion = "Write recursive function for the Fibonacci series and return the value of the 15th element"
        }
        {
            SampleChatType = SM_CodeEval
            SampleMode = ExplorationMode.Factual
            MaxDocs     = 5
            SampleSysMsg  = "ignored"
            SampleQuestion = "Write a function to download a page from the internet and download 'https://www.google.com' display results"
        }
        {
            SampleChatType = SM_Plain false
            SampleMode = ExplorationMode.Factual
            MaxDocs     = 5
            SampleSysMsg  = defaultSysMessage
            SampleQuestion = "What are the major languages and dialects spoken in France? Only list the language names"
        }
    ]

// default settings file location for local development
let settings = ".fsopenai/ServiceSettings.json" //macos

(*
Optional: Invoke this function to copy the local ServiceSettings.json from 'settings' location (see above)
to a keyvault in Azure (file text is converted to base64 encoded string)
The keyname is from this config file but the KeyVault name comes from an environment variable C.FSOPENAI_AZURE_KEYVAULT
Note: This function will only work if your identity has access to the keyvault - it uses DefaultAzureCredential()
*)
let setCredsPoc() =
    ScriptEnv.Secrets.setCreds
        keyVault
        keyVaultKey
        settings

(*
Optional: Invoke this function to install meta index in Azure AI Search
*)
let installMetaIndex() =
    ScriptEnv.MetaIndex.loadMeta metaIndexName.Value docs
    printfn $"created meta index {metaIndexName}"


//deploys the configuration and samples files to the correct locations so the running app can find and use them
let run() =

    ScriptEnv.Config.saveConfig acctAppCfg ScriptEnv.Config.CONFIG_PATH
    printfn $"saved config to {ScriptEnv.Config.CONFIG_PATH}"

    ScriptEnv.Config.saveSamples samples SamplesPath

    let clientPath = Path.GetFullPath(__SOURCE_DIRECTORY__ + @$"/{clientFiles}")
    ScriptEnv.Config.installClientFiles clientPath

    let serverPath = Path.GetFullPath(__SOURCE_DIRECTORY__ + @$"/{serverFiles}")
    ScriptEnv.Config.installServerAppSettings serverPath

    ScriptEnv.Config.installTemplates templatesPath

(* uncomment to copy baseSettings file to default location so the running app can find it
*)
File.Copy(
    Utils.homePath.Value @@ ScriptEnv.expandEnv baseSettingsFile,
    Utils.homePath.Value @@ ScriptEnv.expandEnv settings,true )

(*
//run this line to actually deploy, configuration, samples and templates to under wwwroots
run()

//run only if needed
ScriptEnv.installSettings settings //'install' settings so script can use the api keys, etc.
installMetaIndex()

setCredsPoc() // run only if settings have changed

ScriptEnv.Secrets.getCreds keyVault keyVaultKey

//check to see if the meta index is installed correctly
ScriptEnv.Indexes.printMetaIndex [] metaIndexName.Value
*)

