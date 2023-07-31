#r "nuget: System.Text.Json"
open System.IO
open System.Net.Http

let dnld(uri:string) =
    async {
        use wc = new HttpClient()
        let! str = wc.GetStringAsync(uri) |> Async.AwaitTask
        return str
    }
    

let m = dnld "https://google.com"  |> Async.RunSynchronously

m

let txt = File.ReadAllText(@"C:\s\openai\ServiceSettings.json")
let txtArr = System.Text.UTF8Encoding.Default.GetBytes(txt)
let txt64 = System.Convert.ToBase64String(txtArr)







