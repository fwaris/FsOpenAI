﻿#r "nuget: Microsoft.AspNetCore.Components.Web"
#r "nuget: Blazored.LocalStorage"
#r "nuget: Microsoft.Authentication.WebAssembly.Msal"
#r "nuget: Microsoft.Extensions.Http"
#r "nuget: Microsoft.Extensions.Hosting"
#r "nuget: Microsoft.AspNetCore.Components.WebAssembly"
#r "nuget: Microsoft.DeepDev.TokenizerLib"
#r "nuget: Azure.Search.Documents, 11.5.1"
#r "nuget: Microsoft.SemanticKernel, 1.0.1" 
#r "nuget: FSharp.SystemTextJson"
#r "nuget: FSharp.Data"
#r "nuget: FSharp.Collections.ParallelSeq"
#r "nuget: FSharp.Control.AsyncSeq"
#r "nuget: FSharp.Control.TaskSeq, 0.4.0-alpha.1"
#r "nuget: FsPickler.Json"
#r "nuget: Docnet.Core"
#r "nuget: PdfPig, *-*"
#r "nuget: DocumentFormat.OpenXml"
#r "nuget: Bolero" 
#r "nuget: MudBlazor"
#r "nuget: Plotly.NET"

#I "../../FsOpenAI.Client"
#load "Constants.fs"
#load "Utils.fs"
#load "Model/Graph.fs"
#load "Model/AppConfig.fs"
#load "Model/Model.fs"
#load "Model/Interactions.fs"
#load "Model/Initialization.fs"

#I "../../FsOpenAI.Server"
#load "Gen/GenUtils.fs"
#load "Gen/Prompts.fs"
#load "Gen/Indexes.fs"
#load "Gen/SemanticVectorSearch.fs"
#load "Gen/Completions.fs"
#load "Gen/WebCompletion.fs"
#load "Gen/QnA.fs"
#load "Gen/DocQnA.fs"










