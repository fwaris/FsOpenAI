namespace FsOpenAI.GenAI
open System
open System.Net.Http
open System.Text
open System.Text.Json
open Azure.Core
open System.Text.Json.Serialization
open FsOpenAI.Shared
open FsOpenAI.GenAI

type ImageUrl(url:string) =
    member val url : string = url with get, set

[<AbstractClass>]
[<JsonDerivedType(typeof<TextContent>)>]
[<JsonDerivedType(typeof<ImageContent>)>]
type Content(t:string) =
    member val ``type`` : string = t with get, set

and TextContent(text:string) =
    inherit Content("text")
    member val text : string = text with get, set

and ImageContent(data:string) =
    inherit Content("image_url")
    member val image_url = ImageUrl(data) with get, set

type Message (role:string, cs:Content list) =

    member val role : string = role with get, set
    member val content : Content list  = cs with get, set

type Payload(msgs:Message list) =
    member val messages = msgs
    member val temperature = 0.7 with get, set
    member val top_p = 0.95 with get, set
    member val max_tokens = 800 with get,set
    member val stream = false with get, set
    member val model:string = null with get, set

type RMsg =
    {
        role : string
        content : string
    }

type RChoice =  {
    message : RMsg
}

type RUsage =
    {
        completion_tokens : int
        prompt_tokens : int
        total_tokens : int
    }

type Response =
    {
        choices : RChoice list
        usage : RUsage
    }

module VisionApi =
    open System.Net.Http.Headers

    let serOptions() =
        let o = JsonSerializerOptions(JsonSerializerDefaults.General)
        o.WriteIndented <- true
        JsonFSharpOptions.Default()
            .WithAllowNullFields(true)
            .WithAllowOverride(true)
            .AddToJsonSerializerOptions(o)
        o
    let processVision (ep:Uri) (key:string) (user:string) (payload:Payload) =
        task {
            //let url = $"https://{ep.RESOURCE_GROUP}.openai.azure.com/openai/deployments/{model}/chat/completions?api-version=2023-07-01-preview";
            use client = new HttpClient()
            client.DefaultRequestHeaders.Add("api-key",key)
            client.DefaultRequestHeaders.Add("fsopenai-user",user)
            client.DefaultRequestHeaders.Authorization <- new AuthenticationHeaderValue("Bearer", key)
            let productValue = new ProductInfoHeaderValue("fsopenai", "1.0")
            client.DefaultRequestHeaders.UserAgent.Add(productValue)
            let data = JsonSerializer.Serialize(payload,serOptions())
            use content = new StringContent(data, Encoding.UTF8, "application/json")
            let! resp = client.PostAsync(ep,content)
            if resp.IsSuccessStatusCode then
                let! respData = resp.Content.ReadAsStringAsync()
                let resp = JsonSerializer.Deserialize<Response>(respData,serOptions())
                return Some resp
            else
                printfn $"Error: {resp.StatusCode}, {resp.ReasonPhrase}"
                return None
        }

    // let processImage (endpoint:AzureOpenAIEndpoints) (model:string) (sysMsg:string, userPrompt:string, img:byte[]) =
    //     async {
    //         let imageBytes = img |> System.Convert.ToBase64String
    //         let imgUri = $"data:image/jpeg;base64,{imageBytes}"
    //         let chat = [Message("user", [ImageContent(imgUri); TextContent(userPrompt)])]
    //         let chat = if String.IsNullOrWhiteSpace sysMsg |> not then Message("system", [TextContent(sysMsg)])::chat else chat
    //         let payload = Payload(chat)
    //         return! processVision endpoint model payload |> Async.AwaitTask
    //     }

    let processImage (parms:ServiceSettings,invCtx:InvocationContext,backend:Backend) (sysMsg:string, userPrompt:string, img:byte[]) =
        match GenUtils.visionModel backend invCtx.ModelsConfig with
        | Some model ->
            async {
                let endpoint,key = GenUtils.servceEndpoint parms backend model.Model
                let user = GenUtils.userAgent invCtx
                let imageBytes = img |> System.Convert.ToBase64String
                let imgUri = $"data:image/jpeg;base64,{imageBytes}"
                let chat = [Message("user", [ImageContent(imgUri); TextContent(userPrompt)])]
                let chat = if String.IsNullOrWhiteSpace sysMsg |> not then Message("system", [TextContent(sysMsg)])::chat else chat
                let payload = Payload(chat)
                payload.model <- model.Model
                payload.max_tokens <- 2000
                return! processVision (Uri endpoint) key user payload |> Async.AwaitTask
            }
        | None -> async { return failwith "No vision model configured" }
