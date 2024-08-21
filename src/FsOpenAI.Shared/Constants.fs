namespace FsOpenAI.Shared

module C =
    let MAX_INTERACTIONS = 15
    let MAX_DOCLISTS_PER_CHAT = 3 //number of document lists that a chat can maintain (others are dropped as new doc lists are added to chat)

    let CHAT_RESPONSE_TIMEOUT = 1000 * 30 //15 seconds
    let TIMEOUT_MSG = "timeout while waiting for service to respond"

    let LS_OPENAI_KEY = "LS_OPENAI_KEY"
    let MAIN_SETTINGS = "MAIN_SETTINGS"
    let ADD_CHAT_MENU = "ADD_CHAT_MENU"
    let CHATS = "CHATS"

    let DARK_THEME = "DARK_THEME"
    let TABS_UP = "TABS_UP"
    let SIDE_BAR_EXPANDED = "SIDE_BAR_EXPANDED"

    let MAX_UPLOAD_FILE_SIZE = 1024L * 1024L * 14L    
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
    let DFLT_COSMOSDB_NAME = "fsopenai"
    let DFLT_MONITOR_TABLE_NAME = "tmgenai-log1"                    //Note: follow Azure Table naming rules (no underscores or dashes)
    let UNAUTHENTICATED = "Unauthenticated"
    let DFLT_APP_ID = "default"

    let DFLT_ASST_ICON = """<svg version="1.1" viewBox="0 0 76.728 91.282" xmlns="http://www.w3.org/2000/svg">
 <g transform="matrix(.2857 0 0 .2857 71.408 28.262)" fill="#e20074">
  <path d="m-33.599 218.73v-22.192h-15.256c-26.315 0-38.393-15.643-38.393-38.665v-232.6h4.5246c49.283 0 80.582 32.707 80.582 80.797v4.3092h18.745v-107.3h-264.58v107.3h18.745v-4.3092c0-48.09 31.298-80.797 80.582-80.797h4.5246v232.6c0 23.022-12.078 38.665-38.393 38.665h-15.256v22.192z"/>
  <path d="m16.603 111.43h-62.914v-63.129h62.914z"/>
  <path d="m-185.07 111.43h-62.914v-63.129h62.914z"/>
 </g>
</svg>"""

    module ClientHub =
        open System.Threading.Channels
        let fromServer       = "FromServer"
        let fromClient       = "FromClient"
        let fromClientUnAuth = "FromClientUnAuth"
        let uploadStream     = "UploadStream"
        let urlPath          = "/fsopenaihub"

