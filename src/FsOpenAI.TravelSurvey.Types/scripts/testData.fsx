#r "nuget: Fsharp.Data.Csv.Core"
#r "../bin/Debug/net8.0/FsOpenAI.TravelSurvey.Types.dll"

let path = @"E:\s\nhts\csv"

let data = FsOpenAI.TravelSurvey.Data.loadFromPath path



