namespace FsOpenAI.Vision
(* commented out to reduce application package size - to deploy to free azure tier
open OpenCvSharp
open System.Drawing
open System.IO
open FSharp.Control

module Video =    
    //returns the number of frames, fps, width, height and format of the video
    let getInfo f = 
        use clipIn = new VideoCapture(f:string)
        let fc = clipIn.FrameCount
        clipIn.FrameCount,clipIn.Fps,clipIn.FrameWidth,clipIn.FrameHeight,string clipIn.Format

    let readFrame (clipIn:VideoCapture) n =
        async {
            let _ = clipIn.PosFrames <- n
            use mat = new Mat()
            let resp =
                if clipIn.Read(mat) then
                    let ptr = mat.CvPtr
                    use bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat)
                    use ms = new MemoryStream()
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png)
                    ms.Position <- 0L
                    Some(ms.ToArray())
                else
                    None
            mat.Release()
            return resp
        }

    let getFrames file maxFrames = 
        asyncSeq {
            use clipIn = new VideoCapture(file:string)
            let frames = 
                if clipIn.FrameCount <= maxFrames then 
                    [0..clipIn.FrameCount-1] 
                else
                    let skip = clipIn.FrameCount / maxFrames
                    [
                       yield  0                          // keep first
                       for i in 1..maxFrames-1 do        // evenly spaced frames
                           yield i*skip
                       yield clipIn.FrameCount-1         // keep last
                    ]
                    |> set                               // remove duplicates
                    |> Seq.toList
                    |> List.sort
            for n in frames do
                let! frame = readFrame clipIn n
                yield frame
            clipIn.Release()
        }
*)