#r "nuget: Fsharp.Data.Csv.Core"                                                                                              
#r @"c:\Users\Faisa\source\repos\fwaris\FsOpenAI\src\FsOpenAI.TravelSurvey.Types\bin\Debug\net8.0\FsOpenAI.TravelSurvey.Types.dll"                                                                                                                          
                                                                                                                              
module Data =                                                                                                                 
   let load() : Lazy<FsOpenAI.TravelSurvey.Types.DataSets> =                                                                  
    lazy(FsOpenAI.TravelSurvey.Data.loadFromPath  @"E:\s\nhts\csv")   
//*****
open FsOpenAI.TravelSurvey.Types
open Helpers
let data = Data.load().Value                                                                                                  

let averageCommuteTimeForWorkers (data: DataSets) : string =                                                                  
    let commuteTimes =                                                                                                        
        data.Trip                                                                                                             
        |> List.filter (fun (trip: Trip) -> trip.WHYTRP1S = WHYTRP1S_Work)                                                    
        |> List.choose (fun (trip: Trip) ->                                                                                   
            match trip.TRVLCMIN with                                                                                          
            | Value duration -> Some duration                                                                                 
            | _ -> None)                                                                                                      

    let totalCommuteTime = List.sum commuteTimes                                                                              

    let numberOfCommutes = List.length commuteTimes                                                                           

    let averageCommuteTime = float totalCommuteTime / float numberOfCommutes                                                  

    formatNumber averageCommuteTime
averageCommuteTimeForWorkers data                                                                                             
                                                                                                                               
