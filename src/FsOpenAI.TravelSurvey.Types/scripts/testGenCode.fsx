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
                                                                                                                                           
let dataset = Data.load().Value                                                                                                            
                                                                                                                                           
let getPercentageOfTripsByVehicleType () : string =                                                                                        
    let totalTrips = dataset.Trips |> List.length                                                                                          
    let tripsByVehicleType =                                                                                                               
        dataset.Trips                                                                                                                      
        |> List.groupBy (fun (trip: Trip) -> trip.VEHTYPE)                                                                                 
        |> List.map (fun (vehicleType, trips) ->                                                                                           
            let percentage = (float (List.length trips) / float totalTrips) * 100.0                                                        
            vehicleType, percentage                                                                                                        
        )                                                                                                                                  
                                                                                                                                           
    tripsByVehicleType                                                                                                                     
    |> List.map (fun (vehicleType, percentage) ->                                                                                          
        $"{vehicleType}: {formatNumberPercent percentage}"                                                                                 
    )                                                                                                                                      
    |> String.concat "\n"                                                                                                                  
                                                                                                                                           
getPercentageOfTripsByVehicleType ()
