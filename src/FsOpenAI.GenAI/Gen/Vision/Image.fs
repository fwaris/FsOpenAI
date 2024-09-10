namespace FsOpenAI.GenAI.Image
open System
open System.IO

module ImageUtils = 

    let imagePath docPath pageNum imageNum = docPath + $".{pageNum}_{imageNum}.jpeg"
    let imageTextPath imagePath = imagePath + ".txt"

[<RequireQualifiedAccess>]
module Extraction =
    open System.Drawing
    open System.Drawing.Imaging
    open UglyToad.PdfPig
    open DocumentFormat.OpenXml.Packaging
    open DocumentFormat.OpenXml.Presentation

    let exportImagesToDiskWord (filePath:string) =
        try
            use d = WordprocessingDocument.Open(filePath,false)
            d.MainDocumentPart.ImageParts
            |> Seq.iteri(fun i img ->
                let imgPath = ImageUtils.imagePath filePath 0 i
                printfn $"Image {i} : {imgPath}"
                use bmp = Bitmap.FromStream(img.GetStream())
                bmp.Save(imgPath,ImageFormat.Jpeg))
        with ex ->
            printfn $"Error in extractImagesWord {filePath} :  {ex.Message}"

    let exportImagesToDiskPpt (filePath:string) =
        try
            use d = PresentationDocument.Open(filePath,false)
            let images =
                d.PresentationPart.SlideParts
                |> Seq.collect(fun slidePart ->
                    slidePart.Slide.Descendants<Picture>()
                    |> Seq.choose(fun p ->
                        try
                            let part = slidePart.GetPartById(p.BlipFill.Blip.Embed) :?> ImagePart
                            Some part
                        with ex ->
                            printfn $"error extracting as image - part {p.BlipFill.Blip.Embed.InnerText}"
                            None))
                |> Seq.toList
            images
            |> List.iteri (fun i img ->
                let imgPath = ImageUtils.imagePath filePath 0 i
                printfn $"Image {i} : {imgPath}"
                use bmp = Bitmap.FromStream(img.GetStream())
                bmp.Save(imgPath,ImageFormat.Jpeg))
        with ex ->
            printfn $"Error in extractImagesWord {filePath} :  {ex.Message}"


    ///Extracts any jpeg images in pdf and saves to disk.
    ///Image file paths are <input path>_{page#}_{image#}.jpeg
    let exportImagesToDisk (path:string) =
        let pdf = PdfDocument.Open(path);

        let encoders = ImageCodecInfo.GetImageDecoders();
        let jpegEncoder = encoders |> Seq.find (fun enc -> enc.FormatID = ImageFormat.Jpeg.Guid)

        for page in pdf.GetPages() do
            let images = page.GetImages() |> Seq.toArray
            let mutable j = 0
            for i in images do
            let mutable pngBytes: byte[] = Unchecked.defaultof<_>
            let d = i.TryGetPng(&pngBytes)
            use stream = new MemoryStream(if pngBytes = null then i.RawBytes |> Seq.toArray else pngBytes);
            use image = Image.FromStream(stream, false, false);
            let fn = ImageUtils.imagePath path page.Number j
            image.Save(fn, jpegEncoder, null);
            j <- j + 1

        Console.WriteLine($"Extracted images from {path}")


    ///Utility function to list jpeg images contained in a pdf file. Can be used to check if file has any embedded images.
    ///Returns: page#, image#, PdfDictionary (which can be used with exportImage function to save image to disk)
    let listImages (path:string) =
        seq {
            use pdf = PdfDocument.Open(path);
            for page in pdf.GetPages() do
                let images = page.GetImages() |> Seq.indexed
                for (i,img) in images do
                    yield page.Number, i, img

        }
        |> Seq.toList

[<RequireQualifiedAccess>]
module Conversion =
    open System.Drawing
    open System.Drawing.Imaging

    let addBytes (bitmap:Bitmap) (bytes:byte[]) =
        let rect = new Rectangle(0,0,bitmap.Width,bitmap.Height)
        let bmpData = bitmap.LockBits(rect,ImageLockMode.ReadWrite,bitmap.PixelFormat)
        let ptr = bmpData.Scan0
        let bytesCount = bytes.Length
        System.Runtime.InteropServices.Marshal.Copy(bytes,0,ptr,bytesCount)
        bitmap.UnlockBits(bmpData)

    let jpegEncoder (mimeType:string) =
        ImageCodecInfo.GetImageEncoders()
        |> Array.tryFind (fun codec -> codec.MimeType = mimeType)

    let saveBmp (bmp:Bitmap) (outPath:string) =
        let qualityEncoder = Encoder.Quality;
        use qualityParameter = new EncoderParameter(qualityEncoder, 90);
        use encoderParms = new EncoderParameters(1)
        encoderParms.Param.[0] <- qualityParameter
        let codec = jpegEncoder "image/jpeg" |> Option.defaultWith (fun _ -> failwith "jpeg codec not found")
        bmp.Save(outPath,codec,encoderParms)

    ///Export entire pages as jpeg images to disk
    ///Image file paths are <input path>_{page#}_0.jpeg
    let exportImagesToDisk (backgroundRGB:(byte*byte*byte) option) (path:string) =
        use inst = Docnet.Core.DocLib.Instance
        use reader = inst.GetDocReader(path,Docnet.Core.Models.PageDimensions(1.0))
        [0 .. reader.GetPageCount()-1]
        |> List.iter (fun i ->
            use page = reader.GetPageReader(i)
            let imgBytes =
                match backgroundRGB with
                | Some (red,green,blue) ->  page.GetImage(new Docnet.Core.Converters.NaiveTransparencyRemover(red,blue,green))
                | None                  ->  page.GetImage()
            let w,h = page.GetPageWidth(),page.GetPageHeight()
            use bmp = new Bitmap(w,h,PixelFormat.Format32bppArgb)
            addBytes bmp imgBytes
            let outPath = ImageUtils.imagePath path i 0
            saveBmp bmp outPath)

    ///Export entire pages as jpeg images to disk
    ///Image file paths are <input path>_{page#}_0.jpeg
    let exportImagesToDiskScaled (backgroundRGB:(byte*byte*byte) option) (scale:float) (path:string) =
        use inst = Docnet.Core.DocLib.Instance
        use reader = inst.GetDocReader(path,Docnet.Core.Models.PageDimensions(scale))
        [0 .. reader.GetPageCount()-1]
        |> List.iter (fun i ->
            use page = reader.GetPageReader(i)
            let imgBytes =
                match backgroundRGB with
                | Some (red,green,blue) ->  page.GetImage(new Docnet.Core.Converters.NaiveTransparencyRemover(red,blue,green))
                | None                  ->  page.GetImage()
            let w,h = page.GetPageWidth(),page.GetPageHeight()
            use bmp = new Bitmap(w,h,PixelFormat.Format32bppArgb)
            addBytes bmp imgBytes
            let outPath = ImageUtils.imagePath path i 0
            saveBmp bmp outPath)
