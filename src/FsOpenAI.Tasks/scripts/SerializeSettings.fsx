#load "packages.fsx"
#r "nuget: Azure.Identity"
#r "nuget: azure.security.keyvault.secrets"

open System
open System.IO
open FsOpenAI.Client
open Azure.Identity;
open Azure.Security.KeyVault.Secrets

let KeyVault = System.Environment.GetEnvironmentVariable(C.FSOPENAI_AZURE_KEYVAULT)

let kvUri keyVault = $"https://{keyVault}.vault.azure.net";

let setCreds keyVault keyName settingsFile = 
    let settingsFile  = Environment.ExpandEnvironmentVariables(settingsFile)
    let txt = File.ReadAllText settingsFile
    let txtArr = System.Text.UTF8Encoding.Default.GetBytes(txt)
    let txt64 = System.Convert.ToBase64String(txtArr)
    let kvUri = kvUri keyVault
    let c = new DefaultAzureCredential()
    let client = new SecretClient(new Uri(kvUri), c);            
    let r = client.SetSecret(keyName,txt64)
    printfn "%A" r
    printfn $"set {keyVault}/{keyName} to {settingsFile}"

let getCreds keyVault keyName = 
    printfn "getting ..."
    let kvUri = kvUri keyVault
    let c = new DefaultAzureCredential()
    let client = new SecretClient(new Uri(kvUri), c);        
    let r = client.GetSecret(keyName)
    let txt64 = r.Value.Value
    let json = txt64 |> Convert.FromBase64String |> System.Text.UTF8Encoding.Default.GetString
    let sttngs = System.Text.Json.JsonSerializer.Deserialize<FsOpenAI.Client.ServiceSettings>(json)
    sttngs    


let setCredsProd() = setCreds KeyVault "fsopenaiacct" "%USERPROFILE%/.fsopenai/prod/ServiceSettings.json"

let setCredsPoc() = setCreds KeyVault "fsopenai1" "%USERPROFILE%/.fsopenai/poc/ServiceSettings.json"

let setCredsGC() = setCreds KeyVault "fsopenaigc" "%USERPROFILE%/.fsopenai/gc/ServiceSettings.json"

let pocSttngs() = getCreds KeyVault "fsopenai1"

let devSttngs() = getCreds KeyVault "fsopenaiacct"

let gcSttngs() = getCreds KeyVault "fsopenaigc"




