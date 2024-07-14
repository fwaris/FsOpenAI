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

let calculateLoopTripPercentage () : string =
    let data = Data.load().Value
    let trips = data.Trips

    let totalTrips = trips |> List.length
    let loopTrips = trips |> List.filter (fun (trip: Trip) -> trip.LOOP_TRIP = YesNo.Yes) |> List.length

    let loopTripPercentage = (float loopTrips / float totalTrips) * 100.0
    let formattedPercentage = formatNumberPercent loopTripPercentage

    $"The percentage of loop trips is {formattedPercentage}."

calculateLoopTripPercentage ()
Regenerating... after error: [|input.fsx (6,80)-(6,89) typecheck error This expression was expected to have type
    'LOOP_TRIP'
but here has type
    'YesNo'    |]
open FsOpenAI.TravelSurvey.Types
open FsOpenAI.TravelSurvey.Types.Helpers

let calculateLoopTripPercentage () : string =
    let data = Data.load().Value
    let trips = data.Trips

    let totalTrips = trips |> List.length
    let loopTrips = trips |> List.filter (fun (trip: Trip) -> trip.LOOP_TRIP = YesNo.Yes) |> List.length

    let loopTripPercentage = (float loopTrips / float totalTrips) * 100.0
    let formattedPercentage = formatNumberPercent loopTripPercentage

    $"The percentage of loop trips is {formattedPercentage}."

calculateLoopTripPercentage ()
val it: string =
  "Error [|input.fsx (6,80)-(6,89) typecheck error This expression was expected to have type
    'LOOP_TRIP'
but here has type
    'YesNo'    |]"
