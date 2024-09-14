#r "nuget: System.Text.Json"
open System
open System.Text.Json
open System.IO
open System.Net.Http

//List all models available - for the configured openai key

let toDate (d:int64) = DateTimeOffset.FromUnixTimeSeconds(d).Date

let jsonDoc =
    use wc = new HttpClient()
    let key = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    wc.DefaultRequestHeaders.Authorization <- System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key)
    wc.GetStringAsync("https://api.openai.com/v1/models") |> Async.AwaitTask |> Async.RunSynchronously

let doc = System.Text.Json.JsonDocument.Parse(jsonDoc);;
;;
doc.RootElement.EnumerateObject()
|> Seq.collect(fun d -> if d.Name = "data" then d.Value.EnumerateArray() |> Seq.cast<JsonElement> else Seq.empty)
|> Seq.map(fun n -> n.GetProperty("id").GetString(), n.GetProperty("created").GetInt64() |> toDate) 
//|> Seq.filter (fun (m,d) -> m.StartsWith("gpt"))
|> Seq.sortByDescending snd
|> Seq.iter (fun (m,d) -> printfn "%s" $"{m}: {d.ToShortDateString()}")


