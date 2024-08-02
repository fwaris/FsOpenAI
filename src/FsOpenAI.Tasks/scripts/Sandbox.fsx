module JsonWebToken =
    open System
    open System.Text
    open System.Text.RegularExpressions
    open System.Security.Cryptography
    let replace (oldVal: string) (newVal: string) = fun (s: string) -> s.Replace(oldVal, newVal)
    let minify = 
        let regex = Regex("(\"(?:[^\"\\\\]|\\\\.)*\")|\\s+", RegexOptions.Compiled|||RegexOptions.CultureInvariant)
        fun s ->
            regex.Replace(s, "$1")
    let base64UrlEncode bytes =
        Convert.ToBase64String(bytes) |> replace "+" "-" |> replace "/" "_" |> replace "=" ""
    let decode (s:string) = s |> replace "-" "+" |> replace "_" "/"  |> Convert.FromBase64String
    type IJwtAuthority =
        inherit IDisposable
        abstract member IssueToken: header:string -> payload:string -> string
        abstract member VerifyToken: string -> bool
    let newJwtAuthority (initAlg: byte array -> HMAC) key =
        let alg = initAlg(key)
        let encode = minify >> Encoding.UTF8.GetBytes >> base64UrlEncode
        let issue header payload =
            let parts = [header; payload] |> List.map encode |> String.concat "."
            let signature = parts |> Encoding.UTF8.GetBytes |> alg.ComputeHash |> base64UrlEncode
            [parts; signature] |> String.concat "."
        let verify (token: string) =
            let secondDot = token.LastIndexOf(".")
            let parts = token.Substring(0, secondDot)
            let signature = token.Substring(secondDot + 1)
            (parts |> Encoding.UTF8.GetBytes |> alg.ComputeHash |> base64UrlEncode) = signature

        {
            new IJwtAuthority with
                member this.IssueToken header payload = issue header payload
                member this.VerifyToken token = verify token
                member this.Dispose() = alg.Dispose()
        }

open System.Text
open System.Security.Cryptography
open JsonWebToken
// let header1 = 
//     """{
//         "alg": "HS256",
//         "typ": "JWT"
//     }"""
// let payload1 = 
//     """{
//         "sub": "1234567890",
//         "name": "John Doe",
//         "admin": true
//     }"""
// let encodedSecret = "secret" |> Encoding.UTF8.GetBytes
// let testAuth = newJwtAuthority (fun key -> new HMACSHA256(key) :> HMAC) encodedSecret
// let token1 = testAuth.IssueToken header1 payload1
// testAuth.VerifyToken token1
// testAuth.Dispose()

let token = "eyJ0eXAiOiJKV1QiLCJub25jZSI6InRqOVpDcUZrM1p5RkdXd04tNkZOTEQtTmhZVHZPY1JjZVBFVURlV2M0aGsiLCJhbGciOiJSUzI1NiIsIng1dCI6Ik1HTHFqOThWTkxvWGFGZnBKQ0JwZ0I0SmFLcyIsImtpZCI6Ik1HTHFqOThWTkxvWGFGZnBKQ0JwZ0I0SmFLcyJ9.eyJhdWQiOiIwMDAwMDAwMy0wMDAwLTAwMDAtYzAwMC0wMDAwMDAwMDAwMDAiLCJpc3MiOiJodHRwczovL3N0cy53aW5kb3dzLm5ldC82MmYwMGY5My1hYmU0LTQ0NGMtYTJmNC0wMmIyZGUxMjc5NDMvIiwiaWF0IjoxNzIyNTQyNDgwLCJuYmYiOjE3MjI1NDI0ODAsImV4cCI6MTcyMjU0NzIyNywiYWNjdCI6MCwiYWNyIjoiMSIsImFpbyI6IkFZUUFlLzhYQUFBQXFLdXMzQUJzb1FtaFZIQ0M4dkYrSzBwUW13djJhSWw4Z2dlaUN4VUt6NkIrRzRDWElDZnhSSGtKU3kvNXpoL2xNaGM2clJldEVHV29CYThGR2ZXQWFLWjZpWm83aWJ0QjE5SjZTSDR6TFJOUHFpRjI3b1h5K0VBVHhEbmpiVUI4UWpjZzNFQkgrKzI2UGE0WUJtM3NTVFV3Q0FBT2lOaWZJN3VpQXJlcFVTbz0iLCJhbHRzZWNpZCI6IjE6bGl2ZS5jb206MDAwMzQwMDE4NkVCOEQ3MyIsImFtciI6WyJwd2QiLCJtZmEiXSwiYXBwX2Rpc3BsYXluYW1lIjoiZnNfb3BlbmFpX2FwaSIsImFwcGlkIjoiZTZmMTBmM2ItY2JiNC00NGU1LWI1YjYtMjdkZDMyMTdlOWJiIiwiYXBwaWRhY3IiOiIwIiwiZW1haWwiOiJGYWlzYWxXYXJpc0BsaXZlLmNvbSIsImZhbWlseV9uYW1lIjoiV2FyaXMiLCJnaXZlbl9uYW1lIjoiRmFpc2FsIiwiaWRwIjoibGl2ZS5jb20iLCJpZHR5cCI6InVzZXIiLCJpcGFkZHIiOiIyMy4yOC4xNDQuMTk1IiwibmFtZSI6IkZhaXNhbCBXYXJpcyIsIm9pZCI6ImM5NWZmMmE2LTc0NzItNDlhNi1hOGI1LWU4MWNkOWYzYjU0ZSIsInBsYXRmIjoiMyIsInB1aWQiOiIxMDAzMjAwMTE3NzdGMzA3IiwicmgiOiIwLkFYd0Frd193WXVTclRFU2k5QUt5M2hKNVF3TUFBQUFBQUFBQXdBQUFBQUFBQUFCOEFBby4iLCJzY3AiOiJvcGVuaWQgcHJvZmlsZSBVc2VyLlJlYWQgZW1haWwiLCJzaWduaW5fc3RhdGUiOlsia21zaSJdLCJzdWIiOiJBUGRLTE91RFF2dENYQ002WGN6djRsUXEzeDN6bG82Q2ZlTUFGUzUzYXFnIiwidGVuYW50X3JlZ2lvbl9zY29wZSI6Ik5BIiwidGlkIjoiNjJmMDBmOTMtYWJlNC00NDRjLWEyZjQtMDJiMmRlMTI3OTQzIiwidW5pcXVlX25hbWUiOiJsaXZlLmNvbSNGYWlzYWxXYXJpc0BsaXZlLmNvbSIsInV0aSI6IkFOUDVSaGpUMGtXdUNZd0pXNTFfQUEiLCJ2ZXIiOiIxLjAiLCJ3aWRzIjpbIjYyZTkwMzk0LTY5ZjUtNDIzNy05MTkwLTAxMjE3NzE0NWUxMCIsImI3OWZiZjRkLTNlZjktNDY4OS04MTQzLTc2YjE5NGU4NTUwOSJdLCJ4bXNfaWRyZWwiOiIxIDQiLCJ4bXNfc3QiOnsic3ViIjoiT1FqT1BDanVlWkxidE9wWlhCYXV1cUttNHRNUnFiTU9Ebm5CQ0d4VDJFdyJ9LCJ4bXNfdGNkdCI6MTYxMzUzMjg1OH0.RUbmtpwGNeCSD5sa8Pvbi8Jon2HJJgLbHbmiCpZ9XDUDwS8WmzsoYpcwkZuPK7KelnnSGJTHIgijm3pUG5CI-K2zqEqxua6_VN8HB2lHU-Q5C7SSHJ9cFgZs1KNrjGeHpv3ekpjDX4S7y-lsw5Fu3NGOutjFzGV65aEk7DvS3iiabp65yw9dAlQmoU9FhOIp3Ja2BnizJf2-DAbiy-4ZY1jvmYjlWJl4-Y5wD3SDXW9Zu2FqiiiYFnqS4p-TwNYBcEroWGE5lWCI8zqg49gHf2pbTwISkGphQOX6AfBKopcLNQHZHppHmEPf0kTxKgyWBplhEC1GMJpaYWDnYbeuDw"
let firstDoc = token.IndexOf(".")
let secondDot = token.LastIndexOf(".")
let parts = token.Substring(0, secondDot)
let signature = token.Substring(secondDot + 1)
let hdr = token.Substring(0, firstDoc)
let payload = 
    let p = token.Substring(firstDoc + 1, secondDot - firstDoc - 1) 
    let ending = [for _ in 0 .. ((p.Length % 3) - 1) -> "="] |> String.concat ""
    p + ending
decode payload |> Encoding.UTF8.GetString |> printfn "%s"
decode hdr |> Encoding.UTF8.GetString |> printfn "%s"
