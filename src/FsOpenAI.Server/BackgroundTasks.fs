namespace FsOpenAI.Server
open System
open System.Threading
open System.IO
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open FsOpenAI.Shared
open FsOpenAI.GenAI

//Start background activities at startup
type BackgroundTasks(logger:ILogger<BackgroundTasks>) =
    inherit BackgroundService()
    let mutable cts : CancellationTokenSource = Unchecked.defaultof<_>

    let dispose() =
        if cts <> Unchecked.defaultof<_> then 
            cts.Dispose()
            cts <- Unchecked.defaultof<_>

    let scan() =
        logger.LogInformation("starting scan")
        try
            Directory.GetFiles(Path.GetTempPath(), $"*.{C.UPLOAD_EXT}")
            |> Seq.append(Directory.GetFiles(Path.GetTempPath(),"*.fsx"))
            |> Seq.iter(fun fn -> 
                try 
                    let fi = FileInfo(fn)
                    if fi.LastWriteTime < DateTime.Now.Add(C.UPLOAD_FILE_STALENESS) then
                        File.Delete(fn)
                        logger.LogInformation($"removed stale file {fn}")
                with ex -> 
                    logger.LogError(ex,$"delete stale file: {fn}")
            )
        with ex -> 
            logger.LogError(ex,"scan")
    
    ///remove any dangling uploaded or code eval files in the temp folder
    let startScanning() = 
        cts <- new CancellationTokenSource()
        let scanner =
            async{
                while not cts.IsCancellationRequested do
                    do! Async.Sleep C.SCAN_PERIOD
                    scan()
            }
        Async.Start(scanner,cts.Token)

    let connectServices() = 
        async {
            try Monitoring.ensureConnection() with ex -> 
                Env.logException (ex,"monitor")
            try Sessions.ensureConnection() with ex ->  
                Env.logException (ex,"connection")
        }
        |> Async.Start

    override this.Dispose(): unit = dispose()

    override this.ExecuteAsync(stoppingToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        Tasks.Task.CompletedTask
    
    override this.ExecuteTask: System.Threading.Tasks.Task =
        Tasks.Task.CompletedTask
    
    override this.StartAsync(cancellationToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        startScanning()
        connectServices()
        Tasks.Task.CompletedTask
    
    override this.StopAsync(cancellationToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        dispose()
        Tasks.Task.CompletedTask