module SimpleCrypt
open System
open System.Text

let xorer (key:byte[]) = fun i c -> c ^^^ key.[i%key.Length]

let encr key (s:string) = s |> Encoding.Default.GetBytes |> Array.mapi (xorer key) |> Convert.ToBase64String
let decr key (s:string) = s |> Convert.FromBase64String |> Array.mapi (xorer key)  |> Encoding.Default.GetString

let encrDflt tx =
    let k = Environment.GetEnvironmentVariable("SC_KEY")
    let ks = Convert.FromBase64String(k)
    encr ks tx

let decrDflt tx =
    let k = Environment.GetEnvironmentVariable("SC_KEY")
    let ks = Convert.FromBase64String(k)
    decr ks tx
    

(* generate random key
#load "SimpleCrypt.fs"
let rnd = System.Random()
let bytes = Array.create 16 0uy
let ks = Convert.ToBase64String(bytes)
Convert.FromBase64String(ks)
rnd.NextBytes(bytes)

printfn "%A" bytes

let a1 = encrDflt ""
decrDflt a1
*)
(* usage
SimpleCrypt.encr "data to be encrypted"
SimpleCrypt.decr "<encrypted data>"
*)

