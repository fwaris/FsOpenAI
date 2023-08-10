namespace FsOpenAI.Client

module C =
    let LS_OPENAI_KEY = "LS_OPENAI_KEY"
    let MAIN_SETTINGS = "MAIN_SETTINGS"
    let CHATS = "CHATS"
    let CHAT_DOCS chatId = $"{chatId}_docs"

    let SETTINGS_FILE = "Parms:Settings"                            //1st: look for a reference to settings json file in appSettings
    let FSOPENAI_AZURE_KEYVAULT = "FSOPENAI_AZURE_KEYVAULT"         //2nd: look for it in Azure key vault with name configured as this,
    let FSOPENAI_AZURE_KEYVAULT_KEY = "FSOPENAI_AZURE_KEYVAULT_KEY" //     and key vault key name configured as this
                                                                    //     Note: key vault key value is a base64 encoded json string


