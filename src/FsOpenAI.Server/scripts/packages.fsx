#r "nuget: FSharp.SystemTextJson"
#r "nuget: FSharp.Data"
#r "nuget: FSharp.Collections.ParallelSeq"
#r "nuget: FSharp.Control.AsyncSeq"
#r "nuget: Microsoft.SemanticKernel, 1.0.0-beta2" 
#r "nuget: Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch, 1.0.0-beta2" 
#r "nuget: Docnet.Core"
#r "nuget: PdfPig, *-*"
#r "nuget: Microsoft.AspNetCore.Components.Web"
#r "nuget: Microsoft.Authentication.WebAssembly.Msal"
#r "nuget: Microsoft.Extensions.Http"
#r "nuget: Microsoft.AspNetCore.Components.WebAssembly"
#r "nuget: Bolero" 
#r "nuget: MudBlazor"
#r "nuget: Microsoft.DeepDev.TokenizerLib"

#I "../../FsOpenAI.Client"
#load "Constants.fs"
#load "Utils.fs"
#load "Model/Graph.fs"
#load "Model/AppConfig.fs"
#load "Model/Model.fs"
#load "Model/Interactions.fs"
#load "Gen/GenUtils.fs"
#load "Gen/Prompts.fs"
#load "Gen/Indexes.fs"
#load "Gen/SemanticVectorSearch.fs"
#load "Gen/Completions.fs"
#load "Gen/WebCompletion.fs"
#load "Gen/QnA.fs"
#load "Gen/DocQnA.fs"









