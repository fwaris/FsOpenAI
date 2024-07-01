#r "nuget: Fsharp.Data.Csv.Core"                                                                                              
#r @"c:\Users\Faisa\source\repos\fwaris\FsOpenAI\src\FsOpenAI.TravelSurvey.Types\bin\Debug\net8.0\FsOpenAI.TravelSurvey.Types.dll"                                                                                                                          
open FsOpenAI.TravelSurvey.Types
open FsOpenAI.TravelSurvey.Types
module Data =                                                                                                                 
   let load() : Lazy<FsOpenAI.TravelSurvey.Types.DataSets> =                                                                  
    lazy(FsOpenAI.TravelSurvey.Data.loadFromPath  @"E:\s\nhts\csv")   
//*****

open FsOpenAI.TravelSurvey.Types
open FsOpenAI.TravelSurvey.Helpers

let mostCommonReasonsForTravelDuringWeekends () : string =
    let data = Data.load().Value
    let weekendDays = set [ TRAVDAY.Saturday; TRAVDAY.Sunday ]

    let weekendTrips =
        data.Trips
        |> List.filter (fun (trip: Trip) -> weekendDays.Contains(trip.TRAVDAY))

    let reasonCounts =
        weekendTrips
        |> List.groupBy (fun (trip: Trip) -> trip.WHYTRP1S)
        |> List.map (fun (reason, trips) -> reason, List.length trips)
        |> List.sortByDescending snd

    let mostCommonReason, count = List.head reasonCounts
    let totalTrips = List.length weekendTrips
    let percentage = (float count / float totalTrips) * 100.0

    $"The most common reason for travel during weekends is {mostCommonReason} with {Helpers.formatNumber (float count)} trips, which is {Helpers.formatNumberPercent percentage} of all weekend trips."

mostCommonReasonsForTravelDuringWeekends ()
