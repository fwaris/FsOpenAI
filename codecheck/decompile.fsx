//need ILSpy tool installed 'dotnet tool install -g ilspycmd'
//decompiles FsOpenAI.*.dll files into equivalent C# code
open System.IO

let outputDir = 
    let dir = __SOURCE_DIRECTORY__ + "/../../FsOpenAI.CodeCheck"
    Path.GetFullPath(dir)

if Directory.Exists(outputDir) then
    Directory.Delete(outputDir, true) |> ignore

if not (Directory.Exists(outputDir)) then
    Directory.CreateDirectory(outputDir) |> ignore

let inputDir = 
    let dir = __SOURCE_DIRECTORY__ + @"/../src/FsOpenAI.Server/bin/Debug/net8.0"
    Path.GetFullPath dir

let fsFiles = Directory.GetFiles(inputDir,"FsOpenAI.*.dll")

let decompile (fsFile:string) =
    printfn $"Converting {fsFile} to C#"
    let output = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(fsFile) + ".fs")
    let psi = new System.Diagnostics.ProcessStartInfo()
    psi.FileName <- "ilspycmd"
    psi.Arguments <- $"--disable-updatecheck --nested-directories -p -o {outputDir} {fsFile}"
    psi.UseShellExecute <- false
    psi.RedirectStandardOutput <- true
    let p = System.Diagnostics.Process.Start(psi)
    p.WaitForExit()
;;
fsFiles |> Array.iter decompile

