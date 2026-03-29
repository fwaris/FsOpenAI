#r "nuget: SSH.Net"
open System.IO
open Renci.SshNet

let upload (localFile:string) (remoteFile:string) (host:string) (userId:string) (pwd:string) = 
    use scp = new ScpClient(host,22,userId,pwd)
    try 
        scp.Connect()
        scp.Upload(FileInfo localFile, remoteFile)
    finally
        scp.Disconnect()

let download (remoteFile:string) (localFile:string)  (host:string) (userId:string) (pwd:string) = 
    use scp = new ScpClient(host,22,userId,pwd)
    try 
        scp.Connect()
        scp.Download(remoteFile, FileInfo localFile)
    finally
        scp.Disconnect()



let creds = @"/Users/Faisal.Waris1/Library/CloudStorage/OneDrive-T-MobileUSA/s/legal/creds.txt"
let ls = File.ReadAllLines creds |> Seq.toList
let h,u,p = ls.[0],ls.[1],ls.[2].Trim()

(*
download "/root/ppolllrs0100003.pem" @"/Users/Faisal.Waris1/ppolllrs0100003.pem" h u p

upload @"/Users/Faisal.Waris1/lerweb.tar" "/apps/lerweb.tar" h u p

upload @"/Users/Faisal.Waris1/ppolllrs0100003.pem" "/etc/ssl/ppolllrs0100003.pem" h u p
upload @"/Users/Faisal.Waris1/ppolllrs0100003_pk.pem" "/etc/ssl/ppolllrs0100003_pk.pem" h u p
*)

