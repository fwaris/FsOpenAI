#load "Env.fsx"
open System
open System.IO
open System.Web
open FSharp.Control
open DocumentFormat.OpenXml
open DocumentFormat.OpenXml.Presentation
open DocumentFormat.OpenXml.Wordprocessing
open DocumentFormat.OpenXml.Packaging
type A = DocumentFormat.OpenXml.Drawing.Text
open Docnet.Core
open Env.Index
open Microsoft.SemanticKernel.Text
open MBrace.FsPickler

Env.installSettings "%USERPROFILE%/.fsopenai/gc/ServiceSettings.json"

type DocType = PDF | PPTX | DOCX | AUDIO

let extractTextPptx (pptFile:string) = 
    use d = PresentationDocument.Open(pptFile,false)
    let ids = d.PresentationPart.Presentation.SlideIdList
    ids 
    |> Seq.cast<SlideId>
    |> Seq.map(fun (s:SlideId) ->            
        let part = d.PresentationPart.GetPartById(s.RelationshipId) :?> SlidePart
        let xs = 
            part.Slide.Descendants<A>()
            |> Seq.filter(fun t -> t.Text <> null)
            |> Seq.map(fun t -> t.Text)
        String.Join(" ",xs))
    |> Seq.toList

let extractTextDocx (docFile:string) = 
    use d = WordprocessingDocument.Open(docFile,false)
    let runs = 
        d.MainDocumentPart.Document.Body.Descendants<Run>()
        |> Seq.map(fun r -> r.InnerText)
    String.Join(" ", runs)

let rootFolder = @"C:\s\gc\GCA_Ext\"
let rootUrl = "https://tmobileusa.sharepoint.com/sites/GCAcademy-External/Shared%20Documents"

let (@@) (a:string) (b:string) = Path.Combine(a,b)

let baseLink (path:string) = 
    let p = Path.GetFullPath(path)
    let p = p.Replace(@"\","/")
    let sub = p.Substring(rootFolder.Length)
    let parts = sub.Split("/",StringSplitOptions.RemoveEmptyEntries) |> Array.map (HttpUtility.UrlDecode>>HttpUtility.UrlPathEncode)
    let subClean = String.Join("/",parts)    
    rootUrl + "/" + subClean

let getChunksPdf (i,pdfFile:string) =    
    let baseLink = baseLink pdfFile
    printfn $"reading {i} {pdfFile} - {FileInfo(pdfFile).Length} | {baseLink}"
    use d = DocLib.Instance.GetDocReader(pdfFile,Models.PageDimensions(1.0))
    Env.Index.docTextPdf d
    |> List.map(fun (page,chunk) ->         
        let link = $"{baseLink}#page={page}"
        {File=Path.GetFileName(pdfFile); Page=page; Chunk=chunk; Link=link})

let getChunksPptx (i,pptxFile:string) = 
    let baseLink = baseLink pptxFile
    printfn $"reading {i} {pptxFile} - {FileInfo(pptxFile).Length} | {baseLink}"
    let txs = extractTextPptx pptxFile
    txs
    |> Seq.indexed
    |> Seq.collect(fun (i,t) -> TextChunker.SplitPlainTextParagraphs(ResizeArray [t],1500,100) |> Seq.map(fun c -> i,c))   
    |> Seq.map(fun (slide,chunk) -> 
        //let link = $"{baseLink}#page={page}" ///need to figure out how to link to slides in a ppt
        let link = baseLink
        {File=Path.GetFileName(pptxFile); Page=slide; Chunk=chunk; Link=link})
    |> Seq.toList

let getChunksDocx (i,docxFile:string) =     
    let baseLink = baseLink docxFile
    printfn $"reading {i} {docxFile} - {FileInfo(docxFile).Length} | {baseLink}"
    let t = extractTextDocx docxFile
    TextChunker.SplitPlainTextParagraphs(ResizeArray [t],1500,100) |> Seq.map(fun c -> 0,c)
    |> Seq.map(fun (page,chunk) -> 
        //let link = $"{baseLink}#page={page}" ///need to figure out how to link to slides in a ppt
        let link = baseLink
        {File=Path.GetFileName(docxFile); Page=page; Chunk=chunk; Link=link})
    |> Seq.toList

let navRef (t:TimeSpan) = $$$"""{"referralInfo":{"referralApp":"StreamWebApp","referralView":"ShareDialog-Link","referralAppPlatform":"Web","referralMode":"view"},"playbackOptions":{"startTimeInSeconds":{{{t.TotalSeconds}}} }}"""

let getChunksAudio (i,jsonFile:string) = 
    let mp4File = Path.Combine(Path.GetDirectoryName(jsonFile),Path.GetFileNameWithoutExtension(jsonFile) + ".mp4")
    let baseLink = baseLink mp4File
    printfn $"reading {i} {jsonFile} - {FileInfo(jsonFile).Length} | {baseLink}"
    let audios = 
        try 
            let ser = Json.FsPickler.CreateJsonSerializer()
            use str = File.OpenRead jsonFile
            ser.Deserialize<(TimeSpan*TimeSpan*String) list>(str)
        with ex -> 
            printfn $"skipping {jsonFile} - not transcribed audio file"
            []
    let collSc (t1,t2,s) ls acc = 
        let ys = ((t1,t2,s)::ls) |> List.rev 
        ys 
        |> List.tryHead
        |> Option.map (fun (t1,_,_) -> 
            let tx = ys |> List.map(fun (t1,t2,txt) -> txt)
            let chunk = String.Join(" ",tx)
            (t1,chunk)::acc)
        |> Option.defaultValue acc
    let rec loop textCount subChunks acc (xs:(TimeSpan*TimeSpan*string) list) =
        match xs with 
        | [] -> 
            match subChunks with 
            | [] -> List.rev acc 
            | ((t1,t2,s)::rest)  -> collSc (t1,t2,s) subChunks acc |> List.rev
        | (t1,t2,s)::rest when textCount < 1500 -> loop (textCount + s.Length) ((t1,t2,s)::subChunks) acc rest
        | (t1,t2,s)::rest                       -> loop 0 [] (collSc (t1,t2,s) subChunks acc) rest
    let chunks = loop 0 [] [] audios
    chunks
    |> List.indexed
    |> List.map(fun  (i,(t,chunk)) ->  
        let navRef = navRef t
        let navRefEncoded = navRef |> Text.UTF8Encoding.Default.GetBytes |> Convert.ToBase64String |> HttpUtility.UrlPathEncode
        let link = baseLink + $"?csf=1&web=1&nav={navRefEncoded}"
        {File=Path.GetFileName(jsonFile); Page=i; Chunk=chunk; Link=link})

    
let getPdfs folder = Directory.GetFiles(folder,"*.pdf", EnumerationOptions(RecurseSubdirectories=true))
let getPptxs folder = Directory.GetFiles(folder,"*.pptx", EnumerationOptions(RecurseSubdirectories=true))
let getDocx folder = Directory.GetFiles(folder,"*.docx", EnumerationOptions(RecurseSubdirectories=true))
let getAudios folder = Directory.GetFiles(folder,"*.json", EnumerationOptions(RecurseSubdirectories=true))


let toAsyncSeq xs = if Array.length xs = 0 then AsyncSeq.empty else AsyncSeq.ofSeq xs

let processFolder (docSets:Set<DocType>) (k:string,v) = 
    let indexDef = Env.Index.indexDefinition (k.ToLower())
    let pdfs = if docSets.Contains PDF then getPdfs v else [||]
    let pptxs = if docSets.Contains PPTX then getPptxs v else [||]
    let docxs = if docSets.Contains DOCX then getDocx v else [||]
    let audios = if docSets.Contains AUDIO then getAudios v else [||]
    printfn $"{k}, pdfs:{pdfs.Length}, pptxs:{pptxs.Length}, docxs:{docxs.Length}, audios:{audios.Length}"
    let runPdfs =
        pdfs 
        |> Seq.indexed
        |> Seq.collect (getChunksPdf) 
        |> Seq.filter(fun d -> d.Chunk.Trim().Length > 0)
    let runPptxs = 
        pptxs
        |> Seq.indexed
        |> Seq.collect (getChunksPptx) 
        |> Seq.filter(fun d -> d.Chunk.Trim().Length > 0)
    let runDocxs = 
        pptxs
        |> Seq.indexed
        |> Seq.collect (getChunksDocx) 
        |> Seq.filter(fun d -> d.Chunk.Trim().Length > 0)
    let runAudios = 
        audios
        |> Seq.indexed
        |> Seq.collect (getChunksAudio) 
        |> Seq.filter(fun d -> d.Chunk.Trim().Length > 0)
    let recreateIndex = true

    let docsSeq = [runPdfs; runAudios; runPptxs; runDocxs] |> Seq.collect id
    AsyncSeq.ofSeq docsSeq
    |> Env.Index.getEmbeddingsAsync 7.0 
    |> AsyncSeq.map Env.Index.toSearchDoc    
    |> Env.Index.loadIndexAsync recreateIndex indexDef 

let run compute = 
    async {
        let! r = Async.Catch compute
        match r with 
        | Choice1Of2 _ -> printfn "done"
        | Choice2Of2 ex -> printfn $"{ex.Message}"
    }
    |> Async.RunSynchronously


let indexPdfs indexName folder =
    let shredded = Env.Index.shredPdfsAsync folder
    let embedded = Env.Index.getEmbeddingsAsync 7.0 shredded
    let searchDocs = embedded |> AsyncSeq.map Env.Index.toSearchDoc
    let indexDef = Env.Index.indexDefinition indexName
    Env.Index.loadIndexAsync true indexDef searchDocs

let indexToFolderMap = 
    [
        "ericsson-gc-academy", rootFolder @@ "Ericsson Overview"
        "nokia-gc-academy", rootFolder @@ "Nokia Overview"
        "ixr-e-gc-academy", rootFolder @@ "IXR-e Overview"
    ]

let docSet = set [DOCX; PDF; AUDIO]
//let docSet = set [DOCX; PDF; AUDIO; PPTX]

(*
indexToFolderMap.[0] |> processFolder docSet |> run
indexToFolderMap.[1] |> processFolder docSet |> run
indexToFolderMap.[2] |> processFolder docSet |> run 

indexPdfs "t-mobile-standards" @"C:\s\gc\t-mobile_construction"  |> run
indexPdfs "nokia-install" @"C:\s\gc\nokia_construction"          |> run
indexPdfs "ericsson-install" @"C:\s\gc\ericsson_construction"    |> run

*)

open Plotly.NET
let plotDist (t:string) xs = 
    xs 
    |> List.collect id 
    |> List.filter(fun x->x.Chunk.Trim().Length > 0) 
    |> List.map (fun x->x.Chunk.Length) 
    |> Chart.Histogram 
    |> Chart.withTitle t
    |> Chart.show

let testChunking() =
    let i = 1
    let d1 = getPdfs (snd indexToFolderMap.[i]) |> Array.toList |> List.indexed |> List.map getChunksPdf
    let d2 = getPptxs (snd indexToFolderMap.[i]) |> Array.toList |> List.indexed |> List.map getChunksPptx
    let d3 = getDocx (snd indexToFolderMap.[i]) |> Array.toList |> List.indexed |> List.map getChunksDocx
    let d4 = getAudios (snd indexToFolderMap.[i]) |> Array.toList |> List.indexed |> List.map getChunksAudio
    plotDist "pdf" d1
    plotDist "pptx" d2
    plotDist "docx" d3
    plotDist "audio" d4

