namespace FsOpenAI.Vision
open System
open System
open System.IO
open TesseractOCR
open TesseractOCR.Enums
open TesseractOCR.Pix

module OCR =
    open TesseractOCR.Enums

    let processImage (imagePath:string) (trainDataPath:Lazy<string>) =
        async {
            use engine = new Engine(trainDataPath.Value, Language.English, EngineMode.Default);
            use img = Pix.Image.LoadFromFile(imagePath);            
            use page = engine.Process(img)
            return page.Text,page.MeanConfidence
        }

    (* don't use this (img as byte[]) as it might create its own temp files that are not properly cleaned up
    let processImageBytes (img:byte[]) (trainDataPath:string) =
        async {
            use engine = new Engine(trainDataPath,  Language.English, EngineMode.Default);
            use img = Pix.Image.LoadFromMemory(img)
            use page = engine.Process(img)
            let text = page.Text
            return text,page.MeanConfidence
        }
    *)
