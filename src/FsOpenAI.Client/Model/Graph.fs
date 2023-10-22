namespace FsOpenAI.Client.Graph
open System
open System.Net.Http
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.WebAssembly.Authentication

type GraphAPIAuthorizationMessageHandler(p,n) as this =
    inherit AuthorizationMessageHandler(p,n)
    do
        this.ConfigureHandler(
            authorizedUrls = [|"https://graph.microsoft.com"|],
            scopes = [| "https://graph.microsoft.com/User.Read"|]
            )
            |> ignore

module Api =
    open System.Net.Http.Headers
    let CLIENT_ID = "GraphAPI"

    let configure (client:HttpClient) = 
        client.BaseAddress <- new Uri("https://graph.microsoft.com") 

    let getDetails (user,httpFac:IHttpClientFactory) = 
        task {
            let client = httpFac.CreateClient(CLIENT_ID)
            let! ph = client.GetAsync("v1.0/me/photo/$value")
            let! photo =    
                if ph.IsSuccessStatusCode then                     
                    task {
                        let! bytes = ph.Content.ReadAsByteArrayAsync()
                        let str = bytes |> Convert.ToBase64String
                        let str = $"data:image/png;base64,{str}"
                        return Some str
                    }
                else
                    task {return None}
            return photo
        }
