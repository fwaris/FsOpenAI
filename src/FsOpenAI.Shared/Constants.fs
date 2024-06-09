namespace FsOpenAI.Shared

module C =
    let MAX_INTERACTIONS = 15
    let MAX_DOCLISTS_PER_CHAT = 3 //number of document lists that a chat can maintain (others are dropped as new doc lists are added to chat)

    let CHAT_RESPONSE_TIMEOUT = 1000 * 30 //15 seconds
    let TIMEOUT_MSG = "timeout while waiting for service to respond"

    let LS_OPENAI_KEY = "LS_OPENAI_KEY"
    let MAIN_SETTINGS = "MAIN_SETTINGS"
    let CHATS = "CHATS"

    let DARK_THEME = "DARK_THEME"
    let TABS_UP = "TABS_UP"

    let MAX_UPLOAD_FILE_SIZE = 1024L * 1024L * 7L    
    let defaultSystemMessage = "You are a helpful AI Assistant"

    let TEMPLATES_ROOT = "app/Templates"
    let TMPLTS_EXTRACTION = "ExtractionSkill"
    let TMPLTS_DOCQUERY = "DocQuerySkill"

    let SAMPLES_JSON = "Samples.json"
    let APP_CONFIG_PATH = "app/AppConfig.json"

    let DEFAULT_META_INDEX = "fsopenai-meta" //meta index name. allowed: lower case letters; digits; and dashes

    let SETTINGS_FILE = "Parms:Settings"                            //1st: look for a reference to settings json file in appSettings
    let SETTINGS_FILE_ENV = "FSOPENAI_SETTINGS_FILE"                //2nd: look for the whole settings file in this environment variable (base64 encoded json string)
    let FSOPENAI_AZURE_KEYVAULT = "FSOPENAI_AZURE_KEYVAULT"         //3nd: look for a key vault name in this environment variable,
    let FSOPENAI_AZURE_KEYVAULT_KEY = "FSOPENAI_AZURE_KEYVAULT_KEY" //     and key vault key name in this environment variable
                                                                    //     Note: key vault key value is a base64 encoded json string
    let DFLT_MONITOR_TABLE_NAME = "tmgenai-log1"                   //Note: follow Azure Table naming rules (no underscores or dashes)

    module ClientHub =
        open System.Threading.Channels
        let fromServer = "FromServer"
        let fromClient = "FromClient"
        let uploadStream = "UploadStream"
        let urlPath = "/fsopenaihub"

