namespace FsOpenAI.Shared
open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Security.Cryptography

module Utils =
    //let mutable private id = 0
    //let nextId() = Threading.Interlocked.Increment(&id)

    let newId() = 
        Guid.NewGuid().ToByteArray() 
        |> Convert.ToBase64String 
        |> Seq.takeWhile (fun c -> c <> '=') 
        |> Seq.map (function '/' -> 'a' | c -> c)
        |> Seq.toArray 
        |> String

    let notEmpty (s:string) = String.IsNullOrWhiteSpace s |> not
    let isEmpty (s:string) = String.IsNullOrWhiteSpace s 

    let isOpen key map = map |> Map.tryFind key |> Option.defaultValue false

    exception NoOpenAIKey of string

    let serOptions() = 
        let o = JsonSerializerOptions(JsonSerializerDefaults.General)
        o.WriteIndented <- true
        o.ReadCommentHandling <- JsonCommentHandling.Skip
        JsonFSharpOptions.Default()
            .WithAllowNullFields(true)
            .WithAllowOverride(true)
            .WithSkippableOptionFields(false)
            .AddToJsonSerializerOptions(o)        
        o

    let shorten len (s:string) = if s.Length > len then s.Substring(0,len) + "..." else s

    let (@@) a b = System.IO.Path.Combine(a,b)

    let idx (ptrn:string,start:int) (s:string) = 
        let i = s.IndexOf(ptrn,start)
        if i < 0 then None else Some(i)

    let wrapBlock (s:string) = $"<pre><code>{s}</code></pre>"

    let blockQuotes (msg:string) =  
        let i1 = idx ("```",0) msg
        let i2 = i1 |> Option.bind (fun i -> idx ("\n",i) msg)
        let i3 = i2 |> Option.bind (fun i -> idx ("```",i) msg)
        match i1,i2,i3 with
        | None,_,_ -> msg
        | Some i1, None, None -> msg.Substring(0,i1) + (wrapBlock (msg.Substring(i1+3)))
        | Some i1, Some i2, None -> msg.Substring(0,i1) + wrapBlock (msg.Substring(i2))
        | Some i1, Some i2, Some i3 -> msg.Substring(0,i1) + wrapBlock(msg.Substring(i2+1,i3)) + msg.Substring(i3+3)
        | _ -> msg
        

    let genKey () =
        let key = Aes.Create()
        key.GenerateKey()
        key.GenerateIV()
        key.Key,key.IV

    let encrFile (key,iv) (path:string)(outpath) = 
        use enc = Aes.Create()
        enc.Mode <- CipherMode.CBC
        enc.Key <- key
        enc.IV <- iv
        use inStream = new FileStream(path, FileMode.Open)
        use outStream = new FileStream(outpath, FileMode.Create)
        use encStream = new CryptoStream(outStream, enc.CreateEncryptor(), CryptoStreamMode.Write)  
        inStream.CopyTo(encStream)

    let decrFile (key,iv) (path:string) (outpath:string) = 
        use enc = Aes.Create()
        enc.Mode <- CipherMode.CBC
        enc.Key <- key
        enc.IV <- iv
        use inStream = new FileStream(path, FileMode.Open)
        use decrStream = new CryptoStream(inStream, enc.CreateDecryptor(), CryptoStreamMode.Read)  
        use outStream = new FileStream(outpath, FileMode.Create)
        decrStream.CopyTo(outStream)
