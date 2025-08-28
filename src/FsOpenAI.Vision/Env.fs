namespace FsOpenAI.Vision
open System
open System.IO
open System.Reflection

module Env =
    let homePath = lazy(
        match Environment.OSVersion.Platform with 
        | PlatformID.Unix 
        | PlatformID.MacOSX -> Environment.GetEnvironmentVariable("HOME") 
        | _                 -> Environment.GetEnvironmentVariable("USERPROFILE"))

    let LOCAL_TRAIN_DATA_PATH = @"..\data\trainData"
    let DEF_TRAIN_DATA_PATH =  Path.Combine(homePath.Value, "trainData")

    let asmDir = lazy(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))

    let trainDataPath = lazy(
        let path = Path.Combine(asmDir.Value, LOCAL_TRAIN_DATA_PATH)
        if Directory.Exists path then
            path
        else
            DEF_TRAIN_DATA_PATH
    )

