open System.IO

//Example: Encode settings json file to a base64 encoded string so that it can be
//stored in the Azure key vault 


let txt = File.ReadAllText(@"C:\Users\Faisa\.fsopenai\ServiceSettings.json")
let txtArr = System.Text.UTF8Encoding.Default.GetBytes(txt)
let txt64 = System.Convert.ToBase64String(txtArr)
;;
printfn "Encoded settings:"
printfn "%s" txt64

