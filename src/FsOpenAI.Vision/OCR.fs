namespace FsOpenAI.Vision
open System
open System
open System.IO
open TesseractOCR
open TesseractOCR.Enums
open TesseractOCR.Exceptions
open TesseractOCR.Pix

module OCR =

    let processImage (i:int) (imagePath:string) (trainDataPath:Lazy<string>) =
        async {
            use engine = new Engine(trainDataPath.Value, Language.English, EngineMode.Default)
            use img = Pix.Image.LoadFromFile(imagePath)
            use page = engine.Process(img)
            let text = page.Text
            let pout = imagePath + ".0_1.txt"
            File.WriteAllText(pout,text)
        }

    let processImageBytes (img:byte[]) (trainDataPath:string) =
        async {
            use engine = new Engine(trainDataPath, Language.English, EngineMode.Default)
            use img = Pix.Image.LoadFromMemory(img)
            use page = engine.Process(img)
            return page.Text,page.MeanConfidence
        }
