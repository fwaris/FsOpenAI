//find all appsettings.json files in a folder structure and copy them
//to a new folder structure that mimics the original folder structure
//(but only has appsettings.json files)
//Because appsettings.json files are not committed to repo, this script
//is useful for extracting these files and later merging them back
//in into a new local instance of the repo

open System
open System.IO

let inputFolder = @"C:\venv\source\reposTemp\TM_FsOpenAI"
let outputFolder = @"C:\venv\source\reposTemp\TM_FsOpenAI_AppSettings"

let isAppSettings (path:string) = Path.GetFileName(path).Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)
let excludeDirs = set ["bin";"obj";".git";".vs";".vscode";".git"]

let makeLowerCase (path:string) =
    let dir = Path.GetDirectoryName(path)
    let file = Path.GetFileName(path).ToLower()
    Path.Combine(dir, file)

let getAppSettings (root:string) =
    let rec loop acc (path:string) =
        let files = Directory.GetFiles(path, "*.json")
        let files =
            files
            |> Seq.filter (fun f ->
                f.ToLower().Split([|'/';'\\'|])
                |> Array.exists (fun f -> excludeDirs.Contains(f)) |> not)
        let appSettings = files |> Seq.filter isAppSettings |> Seq.toList
        let appSettings = appSettings |> List.map makeLowerCase
        let acc = acc @ appSettings
        let dirs = Directory.GetDirectories(path)
        (acc,dirs) ||> Array.fold loop
    loop [] root

if Directory.Exists(outputFolder) then
    Directory.Delete(outputFolder, true)

for apps in (getAppSettings inputFolder) do
    let relativePath = Path.GetRelativePath(inputFolder, apps)
    let outputPath = Path.Combine(outputFolder, relativePath)
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)) |> ignore
    File.Copy(apps, outputPath, true)
    printfn "Copied %s to %s" apps outputPath
