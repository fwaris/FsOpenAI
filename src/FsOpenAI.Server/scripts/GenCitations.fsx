#r "nuget: FSharp.Data"

open System
open System.IO
open System.Web
open FSharp.Data

let reEncode (sub:string) = 
    let parts = sub.Split("/") |> Array.map (HttpUtility.UrlDecode>>HttpUtility.UrlPathEncode)
    String.Join("/",parts)    

let baseLink (rootUrl:string) (rootFolder:string) (path:string) = 
    let p = Path.GetFullPath(path)
    let p = p.Replace(@"\","/")
    let sub = p.Substring(rootFolder.Length)
    let subClean = reEncode sub 
    rootUrl + "/" + subClean, (Path.GetFileName(path))

let quoted (k:string) = if k.[0] = '"' then k else $"\"{k}\""

let genCitsUsingFolderStructure (rootUrl,rootFolder) = 
    let files = Directory.GetFiles(rootFolder,"*.pdf", EnumerationOptions(RecurseSubdirectories=true)) |> Array.toList
    let links = files |> List.map (baseLink rootUrl rootFolder)
    let links = ("url","doc")::links |> List.map (fun (a,b) -> String.Join(",",[quoted a; quoted b]))
    let citations = Path.Combine(rootFolder,"citations.csv")
    File.WriteAllLines(citations,links)

//doc list is generated from a SharePoint documents collection 
//by using the "Export to Excel" open; and then saved as .csv
type DocList = CsvProvider< @"C:\s\gc\doclist.csv">
let docList = DocList.GetSample().Rows|> Seq.map(fun r->r.Name,r.Path) |> Seq.toList
let tmRoot = @"https://tmobileusa.sharepoint.com"

let getLinks (path:string) =
    let fn = Path.GetFileName(path)
    let mtcs = docList |> List.filter(fun (a,b) -> a=fn)
    mtcs
    |> List.map (fun (d,p) -> (tmRoot + "/" + p + "/" + d) |> reEncode,d)
    |> List.map (fun (a,b) -> String.Join(",", [quoted a; quoted b]))

let genCitesFromDocList folder = 
    let docs = Directory.GetFiles(folder,"*.pdf")
    let cites = Path.Combine(folder,"citations.csv")
    let docLinks = ($"""{quoted "url"},{quoted "doc"}""")::(Seq.toList (docs |> Seq.collect getLinks))
    File.WriteAllLines(cites,docLinks)

//note: there may be duplicates as docs with same file name appear multiple times
genCitesFromDocList @"C:\s\gc\ericsson_construction"
genCitesFromDocList @"C:\s\gc\nokia_construction"
genCitesFromDocList @"C:\s\gc\t-mobile_construction" 

genCitsUsingFolderStructure (
    @"https://tmobileusa.sharepoint.com/sites/NSCPM-NPPM-National-AE-Construction-Standards-External/Shared%20Documents",
    @"C:\s\gc\t-mobile_construction")