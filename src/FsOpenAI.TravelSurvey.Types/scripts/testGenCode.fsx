#r "nuget: Fsharp.Data.Csv.Core"                                                                                              
#r @"c:\Users\Faisa\source\repos\fwaris\FsOpenAI\src\FsOpenAI.TravelSurvey.Types\bin\Debug\net8.0\FsOpenAI.TravelSurvey.Types.dll"                                                                                                                          
                                                                                                                              
module Data =                                                                                                                 
   let load() : Lazy<FsOpenAI.TravelSurvey.Types.DataSets> =                                                                  
    lazy(FsOpenAI.TravelSurvey.Data.loadFromPath  @"E:\s\nhts\csv")   
//*****
open FsOpenAI.TravelSurvey.Types
open Helpers

let mostCommonReasonsForTravelDuringWeekends () : string =
    let data = Data.load().Value
    let weekendDays = set ["Saturday"; "Sunday"]

    let weekendTrips =
        data.Trip
        |> List.filter (fun (trip: Trip) -> weekendDays.Contains(trip.TRAVDAY.ToString()))

    let reasonCounts =
        weekendTrips
        |> List.groupBy (fun (trip: Trip) -> trip.WHYTO)
        |> List.map (fun (reason, trips) -> reason, List.length trips)
        |> List.sortByDescending snd

    let topReasons =
        reasonCounts
        |> List.take 5
        |> List.map (fun (reason, count) -> $"{reason}: {Helpers.formatNumber (float count)}")
        |> String.concat ", "

    $"The most common reasons for travel during weekends are: {topReasons}."

mostCommonReasonsForTravelDuringWeekends ()