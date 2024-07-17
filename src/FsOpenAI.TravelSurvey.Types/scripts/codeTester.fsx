#r "nuget: Fsharp.Data.Csv.Core"                                                                                              
#r @"c:\Users\Faisa\source\repos\fwaris\FsOpenAI\src\FsOpenAI.TravelSurvey.Types\bin\Debug\net8.0\FsOpenAI.TravelSurvey.Types.dll"                                                                                                                         
open FsOpenAI.TravelSurvey.Types
open FsOpenAI.TravelSurvey.Types
module Data =                                                                                                                 
   let load() : Lazy<FsOpenAI.TravelSurvey.Types.DataSets> =                                                                  
    lazy(FsOpenAI.TravelSurvey.Data.loadFromPath  @"E:\s\nhts\csv")   
//*****

open FsOpenAI.TravelSurvey.Types
open FsOpenAI.TravelSurvey.Types.Helpers

let carpoolPercentageForWork () : string =
    let data = Data.load().Value
    let workTrips = data.Trips |> List.filter (fun (trip: Trip) -> trip.TRIPPURP = TRIPPURP.``TRIPPURP_Home-based work (HBW)``)
    let carpoolTrips = workTrips |> List.filter (fun (trip: Trip) -> trip.TRPTRANS = TRPTRANS.``TRPTRANS_Other ride-sharing service``)
    let percentage = (float carpoolTrips.Length / float workTrips.Length) * 100.0
    formatNumberPercent percentage

carpoolPercentageForWork ()
