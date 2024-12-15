namespace FsOpenAI.CodeEvaluator
open FsOpenAI.GenAI
open FsOpenAI.Shared
open FsOpenAI.Shared.Interactions
open FsOpenAI.GenAI.SKernel

module CodeEval =    
    open System.IO
    open System.Threading

    module CodeEval =
        open System.Diagnostics

        type EvalResult = Success of string | Failure of string | Timeout 

        type WorkItem = Async<EvalResult>*AsyncReplyChannel<EvalResult>

        ///worker to serially run the evaluation tasks that arrive in its mailbox (queue)
        let evalWorker (ct:CancellationToken) (inbox:MailboxProcessor<WorkItem>) = 
            async{
                while not ct.IsCancellationRequested do
                    let! task,replyCh = inbox.Receive()
                    try 
                        let! rslt = task
                        replyCh.Reply(rslt)
                    with ex -> 
                        Env.logException(ex,"evalWorker")
                        replyCh.Reply (Failure "Internal error")
            }
        
        ///distribute evaluation tasks among available workers
        let evalRouter (ct:CancellationToken) workers (inbox:MailboxProcessor<WorkItem>) = 
            if List.isEmpty workers then failwith "workers cannot be empty"
            let rec loop (workers:List<MailboxProcessor<WorkItem>>,i) =
                async{
                    let! workItem = inbox.Receive()
                    let _,rc = workItem
                    try 
                        let worker = workers.[i]                    
                        if worker.CurrentQueueLength < 100 then 
                            worker.Post(workItem)
                        else
                            Env.logError("Eval worker queue full")
                            rc.Reply(Failure "Internal error")
                    with ex -> 
                        Env.logException(ex,"evalRouter")                        
                        rc.Reply(Failure "Internal error")
                    if not ct.IsCancellationRequested then
                        return! loop (workers, (i+1) % workers.Length)
                }
            loop (workers,0)

        ///schedule evaluations to run
        let evalScheduler = lazy(
            let ct = Async.DefaultCancellationToken
            let workers = [1 .. Validate.MAX_EVAL_PARALLELISM] |> List.map(fun _ -> MailboxProcessor.Start(evalWorker ct))
            MailboxProcessor.Start(evalRouter ct workers))
        
        ///use time bound external process to evaluate code
        let workItem preamble code = 
            async {
                let code = [preamble; code] |> String.concat("\n")
                let fn = Path.GetTempFileName()
                let fsx = Path.ChangeExtension(fn,".fsx")
                File.WriteAllText(fsx,code)
                let psi = ProcessStartInfo()
                psi.FileName <- "dotnet"
                psi.Arguments <- $"fsi {fsx}"
                psi.UseShellExecute <- false
                psi.CreateNoWindow <- true
                psi.RedirectStandardOutput <- true
                let p = new Process(StartInfo = psi)
                let cts = new CancellationTokenSource()
                cts.Token.ThrowIfCancellationRequested()
                let runProcess() = 
                    async {
                        try
                            cts.CancelAfter(Validate.MAX_CODE_EVAL_TIME_MS)       //start timer                            
                            do! p.WaitForExitAsync(cts.Token) |> Async.AwaitTask
                            try p.Kill(true) with _ -> ()
                            return 0
                        with
                        | ex ->
                            try p.Kill(true) with _ -> ()
                            if cts.IsCancellationRequested then
                                return 1 //timeout
                            else 
                                Env.logException(ex,"CodeEval.workItem")
                                return 2 //other error 
                    }
                let getOutput() = 
                    async {
                        let! str = p.StandardOutput.ReadToEndAsync() |> Async.AwaitTask
                        return str
                    }
                p.Start() |> ignore
                let! job1 = Async.StartChild (runProcess())
                let! job2 = Async.StartChild (getOutput())
                let! r = job1
                let! out = job2
                let out = if Utils.isEmpty out then "Unknown error" else out
                try File.Delete fsx with _ -> ()
                match r with 
                | 0 -> return Success out
                | 1 -> return Timeout
                | 2 -> return Failure out
                | _ -> return failwith "fsiEvalCode: return case not handled"
            }

        ///Schedule evaluation of generated code
        let fsiEvalCode preamble code = 
            evalScheduler.Value.PostAndAsyncReply(fun rc -> workItem preamble code, rc)

        ///Generate code the first time
        let genCode parms invCtx ch dispatch =
            async {
                let! resp = Completions.completeChat parms invCtx ch dispatch None None
                let code = GenUtils.extractCode resp.Content
                Env.logInfo $"Generated id={ch.Id}\n{code}"
                return code
            }

        ///Regenerate code to fix compiler error message. Accepts error message and previously generated code
        let regenCode parms invCtx ch codeParms code errorMessage dispatch =
            async {
                let regenPrompt = codeParms.RegenPrompt |> Option.defaultValue Interactions.CodeEval.CodeEvalPrompts.regenPromptTemplate
                let args = SKernel.kernelArgsDefault ["code",code; "errorMessage",errorMessage]
                let! regenPrompt = SKernel.renderPrompt regenPrompt args |> Async.AwaitTask
                let ch = Interaction.setUserMessage regenPrompt ch
                let ch = codeParms.RegenSystemPrompt 
                        |> Option.map(fun p -> ch |> Interaction.setSystemMessage p) 
                        |> Option.defaultValue ch
                let! resp = Completions.completeChat parms invCtx ch dispatch None None
                let reCode = GenUtils.extractCode resp.Content
                Env.logInfo $"Re-generated id={ch.Id}\n{reCode}"
                return reCode
            }

        let sendResults ch answer dispatch =
            dispatch (Srv_Ia_Delta(ch.Id,answer))
            dispatch (Srv_Ia_Done (ch.Id, None))
         
        let disallowedMessage = "Code evaluation not allowed"
        let timeoutMessage = "Code evaluation exceeded allowed time limit"

        ///attempt to correct generated code. Give up after Validate.MAX_REGEN_ATTEMPTS reached
        let rec tryCorrectCode attempt allowedNamespaces preamble parms invCtx ch codeParms code msg dispatch =
            async {                    
                dispatch (Srv_Ia_Notification (ch.Id, $"Error. Trying to fix and re-evaluate code {attempt} ..."))
                let! newCode = regenCode parms invCtx ch codeParms code msg dispatch
                match Validate.allowedToExec allowedNamespaces preamble newCode with 
                | ChodeCheck_Denied -> dispatch (Srv_Ia_Done (ch.Id, Some disallowedMessage))
                | CodeCheck_Error msg -> 
                        if attempt >= Validate.MAX_REGEN_ATTEMPTS then 
                            dispatch (Srv_Ia_Done (ch.Id, Some msg))
                        else 
                            do! tryCorrectCode (attempt+1) allowedNamespaces preamble parms invCtx ch codeParms newCode msg dispatch
                | CodeCheck_Pass -> 
                    dispatch (Srv_Ia_SetCode (ch.Id, Some code))
                    match! fsiEvalCode preamble newCode with
                    | Success answer -> sendResults ch answer dispatch
                    | Timeout ->  dispatch (Srv_Ia_Done (ch.Id, Some timeoutMessage))
                    | Failure msg ->
                        if attempt >= Validate.MAX_REGEN_ATTEMPTS then 
                            dispatch (Srv_Ia_Done (ch.Id, Some msg))
                        else 
                            do! tryCorrectCode (attempt+1) allowedNamespaces preamble parms invCtx ch codeParms newCode msg dispatch
            }        
                
        let private _genAndEval parms invCtx ch (codeParms:CodeEvalParms) sendResults dispatch =
            async {
                dispatch (Srv_Ia_Notification (ch.Id, $"Calling LLM to generate code..."))
                let ch = Interaction.setSystemMessage (codeParms.CodeGenPrompt |> Option.defaultValue CodeEval.CodeEvalPrompts.sampleCodeGenPrompt) ch
                let preamble,allowedNamespaces = Validate.preamble codeParms.PreambleKey
                let! code = genCode parms invCtx ch dispatch                
                match Validate.allowedToExec allowedNamespaces preamble code with 
                | ChodeCheck_Denied ->
                    Env.logWarning $"Code eval denied id={ch.Id}\n{code}"                    
                    dispatch (Srv_Ia_Done (ch.Id, Some disallowedMessage))
                | CodeCheck_Error msg -> do! tryCorrectCode 1 allowedNamespaces preamble parms invCtx ch codeParms code msg dispatch
                | CodeCheck_Pass -> 
                    dispatch (Srv_Ia_Notification (ch.Id, $"Evaluating code..."))
                    dispatch (Srv_Ia_SetCode (ch.Id, Some code))
                    match! fsiEvalCode preamble code with
                    | Success answer -> sendResults ch answer dispatch
                    | Timeout ->  dispatch (Srv_Ia_Done (ch.Id, Some timeoutMessage))
                    | Failure msg -> do! tryCorrectCode 1 allowedNamespaces preamble parms invCtx ch codeParms code msg dispatch
            }

        let genAndEval parms invCtx ch codeParms dispatch =
            _genAndEval parms invCtx ch codeParms sendResults dispatch

        //evaluation function for testing only
        let genAndEvalTest parms invCtx ch codeParms dispatch =
            let event = new Event<string>()
            let aevent = event.Publish
            let sendResults ch answer dispatch = event.Trigger(answer)   // success case
            let dispatch = function Srv_Ia_Done (chId,Some msg) -> event.Trigger(msg) | x -> dispatch x //failure case
            async {
                do! _genAndEval parms invCtx ch codeParms sendResults dispatch
                let! v = Async.AwaitEvent(aevent)
                return v
            }

    ///Run code generation and evaluation task and dispatch results to client
    let run parms invCtx ch (codeGen:CodeEvalParms) dispatch =
        async {
            try
                do! CodeEval.genAndEval  parms invCtx ch codeGen dispatch
            with ex ->
                GenUtils.handleChatException dispatch ch.Id "CodeEval.run" ex                   
        }
