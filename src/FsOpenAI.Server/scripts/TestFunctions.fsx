#r "nuget: System.Text.Json"

open System.Net.Http

let dnld(uri:string) =
    async {
        use wc = new HttpClient()
        let! str = wc.GetStringAsync(uri) |> Async.AwaitTask
        return str
    }
    

let m = dnld "https://google.com"  |> Async.RunSynchronously

m




