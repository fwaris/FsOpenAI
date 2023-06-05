module FsOpenAI.Server.Services

open System
open Bolero.Remoting.Server
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.SignalR
open FsOpenAI.Client.Subscription

type SubHub() =
    inherit Hub()

    override this.OnConnectedAsync() = 
        let key = System.Environment.GetEnvironmentVariable("SC_KEY")   //generate a new key if requires using see SimpleCrypt.fs
        let apikey = System.Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
        let resourceGroup = System.Environment.GetEnvironmentVariable("AZURE_OPENAI_API_RG")
        let encApiKey = SimpleCrypt.encr (Convert.FromBase64String key) apikey //protects from logging in the browser
        task {
            let! _ = this.SendMessage(SubMsg.SetKey (key,encApiKey,resourceGroup))
            return ()
        }

    member this.SendMessage(msg:SubMsg) =
        let clients = this.Clients       
        task {
            return clients.All.SendAsync("ChartUpdate",msg)
        }