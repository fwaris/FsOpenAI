namespace FsOpenAI.Client

module C =
    let MAX_DOCLISTS_PER_CHAT = 3 //number of document lists that a chat can maintain (others are dropped as new doc lists are added to chat)

    let LS_OPENAI_KEY = "LS_OPENAI_KEY"
    let MAIN_SETTINGS = "MAIN_SETTINGS"
    let CHATS = "CHATS"

    let CHAT_DOCS chatId = $"{chatId}_docs"
    let CHAT_SYS_MSG chatId = $"{chatId}_sysMessage"

    let MAX_UPLOAD_FILE_SIZE = 1024L * 1024L * 5L    
    let defaultSystemMessage = "You are a helpful AI Assistant"

    let TEMPLATES_ROOT = "Templates"
    let TMPLTS_EXTRACTION = "ExtractionSkill"
    let TMPLTS_DOCQUERY = "DocQuerySkill"
    let TMPLTS_DEF_LABEL = "Default"

    let SAMPLES_JSON = "Samples.json"
    let APP_CONFIG_PATH = "Config/AppConfig.json"

    let META_INDEX = "fsopenai-meta-gc" //meta index name. allowed: lower case letters; digits; and dashes

    let SETTINGS_FILE = "Parms:Settings"                            //1st: look for a reference to settings json file in appSettings
    let FSOPENAI_AZURE_KEYVAULT = "FSOPENAI_AZURE_KEYVAULT"         //2nd: look for it in Azure key vault with name configured as this,
    let FSOPENAI_AZURE_KEYVAULT_KEY = "FSOPENAI_AZURE_KEYVAULT_KEY" //     and key vault key name configured as this
                                                                    //     Note: key vault key value is a base64 encoded json string


