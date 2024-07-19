
#r "nuget: Fsharp.Data.Csv.Core"
#r @"c:\Users\Faisa\source\repos\fwaris\fsopenai\src\FsOpenAI.TravelSurvey.Types\bin\Debug\net8.0\FsOpenAI.TravelSurvey.Types.dll"
open FsOpenAI.TravelSurvey.Types
open Helpers

module Data = 
   let load() : Lazy<FsOpenAI.TravelSurvey.Types.DataSets> =  
    lazy(FsOpenAI.TravelSurvey.Data.loadFromPath  @"E:\s\nhts\csv")


/*************
open FsOpenAI.TravelSurvey.Types
open FsOpenAI.TravelSurvey.Types.Helpers

let carpoolPercentage () : string =
    let data = Data.load().Value
    let totalTrips = data.Trips |> List.length
    let carpoolTrips = data.Trips |> List.filter (fun (trip: Trip) -> trip.CARPOOL = CARPOOL.CARPOOL_Yes) |> List.length
    let percentage = (float carpoolTrips / float totalTrips) * 100.0
    formatNumberPercent percentage

carpoolPercentage ()