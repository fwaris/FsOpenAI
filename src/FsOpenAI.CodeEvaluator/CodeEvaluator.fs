namespace FsOpenAI.CodeEvaluator
open FsOpenAI.GenAI
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions

module CodeEval =
    open System
    open System.IO
    open System.Text
    open FSharp.Compiler.Interactive.Shell

    module Evaluation =

        type EvalResult = Success of string | Failure of string

        let splitCode (s:string) =
            let lines =
                seq {
                    use string = new StringReader(s)
                    let mutable line = string.ReadLine()
                    while line <> null do
                        yield line
                        line <- string.ReadLine()
                }
                |> Seq.toList

            let interactiveLines =
                lines
                |> Seq.takeWhile(fun x -> not (x.Trim().StartsWith("let")))
                |> String.concat "\n"

            let expressionLines =
                lines
                |> Seq.skipWhile(fun x -> not (x.Trim().StartsWith("let")))
                |> String.concat "\n"

            interactiveLines,expressionLines

        let evalCode (preamble:string) (code:string) : EvalResult =
            let sbOut = new StringBuilder()
            let sbErr = new StringBuilder()
            use inStream = new StringReader("")
            use outStream = new StringWriter(sbOut)
            use errStream = new StringWriter(sbErr)

            let interactiveLines,expressionLines = splitCode code

            let argv =
                    [|
                        "fsi.exe"
                        "--noninteractive"
                        "--nologo"
                    |]

            let fsiConfig =
                FsiEvaluationSession.GetDefaultConfiguration()

            use fsiSession =
                FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream, collectible=true)

            match fsiSession.EvalInteractionNonThrowing(preamble) with
            | Choice1Of2 v, diag -> printfn "%A; %A" v diag
            | Choice2Of2 e, diag -> printfn "%A; %A" e.Message diag

            match fsiSession.EvalInteractionNonThrowing(interactiveLines) with
            | Choice1Of2 v, diag -> printfn "%A; %A" v diag
            | Choice2Of2 e, diag -> printfn "%A; %A" e.Message diag

            match fsiSession.EvalExpressionNonThrowing(expressionLines) with
            | Choice1Of2 v, diag -> match v.Value.ReflectionValue with
                                    | :? string as ans -> Success ans
                                    | x                -> failwith $"unexpected {x}"
            | Choice2Of2 e, diag -> let msg = sprintf "Evaluation error: %A,%A" e.Message diag
                                    Env.logError msg
                                    Failure ($"%A{diag}")

        let regenCode parms invCtx ch codeParms code errorMessage =
            async {
                let args = GenUtils.kernelArgsDefault ["code",code; "errorMessage",errorMessage]
                let! regenPrompt = GenUtils.renderPrompt codeParms.RegenPrompt args |> Async.AwaitTask
                let ch = Interaction.setUserMessage regenPrompt ch
                let ch = codeParms.RegenSystemPrompt 
                        |> Option.map(fun p -> ch |> Interaction.setSystemMessage p) 
                        |> Option.defaultValue ch
                let! resp = Completions.completeChat parms invCtx ch None
                let reCode = GenUtils.extractCode resp.Content
                return reCode
            }

        let sendResults ch answer dispatch =
            dispatch (Srv_Ia_Delta(ch.Id,0,answer))
            dispatch (Srv_Ia_Done (ch.Id, None))

        let genAndEvalTest parms invCtx ch codeParms =
            async {
                let! resp = Completions.completeChat parms invCtx ch None
                let code = GenUtils.extractCode resp.Content
                printfn "******** code ********"
                printfn "%s" code
                match evalCode codeParms.Preamble code with
                | Success answer -> return answer
                | Failure msg ->
                    let! newCode = regenCode parms invCtx ch codeParms code msg
                    printfn "%s" newCode
                    match evalCode codeParms.Preamble newCode with
                    | Success answer -> return answer
                    | Failure msg ->
                        let! newCode = regenCode parms invCtx ch codeParms code msg
                        printfn "%s" newCode
                        match evalCode codeParms.Preamble newCode with
                        | Success answer -> return answer
                        | Failure msg -> return $"Error {msg}"
            }

        let genAndEval parms invCtx ch codeParms dispatch =
            async {
                dispatch (Srv_Ia_Notification (ch.Id, $"Calling LLM to generate code..."))
                let! resp = Completions.completeChat parms invCtx ch None
                let code = GenUtils.extractCode resp.Content
                printfn "%s" code
                dispatch (Srv_Ia_Notification (ch.Id, $"Evaluing code..."))
                dispatch (Srv_Ia_SetCode (ch.Id, Some code))
                match evalCode codeParms.Preamble code with
                | Success answer -> sendResults ch answer dispatch
                | Failure msg ->
                    dispatch (Srv_Ia_Notification (ch.Id, $"Error evaluating code. Tyring to fix and re-evalute code 1 ..."))
                    let! newCode = regenCode parms invCtx ch codeParms code msg
                    printfn "%s" newCode
                    match evalCode codeParms.Preamble newCode with
                    | Success answer -> sendResults ch answer dispatch
                    | Failure msg ->
                        dispatch (Srv_Ia_Notification (ch.Id, $"Error evaluating code. Tyring to fix and re-evalute code 2 ..."))
                        let! newCode = regenCode parms invCtx ch codeParms code msg
                        printfn "%s" newCode
                        match evalCode codeParms.Preamble newCode with
                        | Success answer -> sendResults ch answer dispatch
                        | Failure msg -> dispatch (Srv_Ia_Done (ch.Id, Some msg))
            }

    let run parms invCtx ch (codeGen:CodeEvalParms) dispatch =
        async {
            try
                do! Evaluation.genAndEval  parms invCtx ch codeGen dispatch
            with ex ->
                Env.logException (ex,"FsOpenAI.CodeEvaluator.API.run")
                dispatch (Srv_Ia_Done (ch.Id, Some $"Unable to process request. Error message: {ex.Message}"))
        }
