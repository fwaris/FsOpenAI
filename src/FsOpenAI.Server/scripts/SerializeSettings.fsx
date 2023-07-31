#r "nuget: System.Text.Json"
#r "nuget: FSharp.Data"
open System.Text.Json

open System.IO
open System.Net.Http

//encode settings
let txt = File.ReadAllText(@"C:\s\openai\ServiceSettings.json")
let txtArr = System.Text.UTF8Encoding.Default.GetBytes(txt)
let txt64 = System.Convert.ToBase64String(txtArr)

let jsonDoc =
    use wc = new HttpClient()
    let key = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    wc.DefaultRequestHeaders.Authorization <- System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key)
    wc.GetStringAsync("https://api.openai.com/v1/models") |> Async.AwaitTask |> Async.RunSynchronously

let doc = System.Text.Json.JsonDocument.Parse(jsonDoc)
doc.RootElement.EnumerateObject()
|> Seq.collect(fun d -> if d.Name = "data" then d.Value.EnumerateArray() |> Seq.cast<JsonElement> else Seq.empty)
|> Seq.map(fun n -> n.GetProperty("id").GetString()) 
|> Seq.toArray

