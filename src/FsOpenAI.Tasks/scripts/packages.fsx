﻿#r "nuget: System.Text.Encoding.CodePages"
#r "nuget: Microsoft.Extensions.Http"
#r "nuget: Microsoft.Extensions.Hosting"
#r "nuget: Microsoft.AspNetCore.Components.WebAssembly"
#r "nuget: Microsoft.Extensions.DependencyInjection"
#r "nuget: Microsoft.ML.Tokenizers.Data.O200kBase"
#r "nuget: Microsoft.Identity.Web, 3.9.3"
#r "nuget: Microsoft.SemanticKernel"
#r "nuget: azure.security.keyvault.secrets"
#r "nuget: Azure.Search.Documents"
#r "nuget: Azure.Identity"
#r "nuget: FSharp.SystemTextJson"
#r "nuget: FSharp.Data"
#r "nuget: FSharp.Collections.ParallelSeq"
#r "nuget: FSharp.Control.AsyncSeq"
#r "nuget: FSharp.CosmosDb"
#r "nuget: FSharp.Compiler.Service"
#r "nuget: FsPickler.Json"
#r "nuget: Docnet.Core"
#r "nuget: PdfPig, *-*"
#r "nuget: DocumentFormat.OpenXml"
#r "nuget: OpenCvSharp4.Windows"
#r "nuget: OpenCvSharp4.Extensions"
#r "nuget: ExcelDataReader"
#r "nuget: ExcelDataReader.DataSet"


//transient packages upgraded to remove security warnings
#r "nuget: System.ClientModel, 1.4.0-beta.1"
#r "nuget: System.Text.RegularExpressions"
#r "nuget: NewtonSoft.json"
#r "nuget: System.Net.Http"
#r "nuget: System.Private.Uri"

#I "../../FsOpenAI.Shared"
#load "Utils.fs"
#load "Constants.fs"
#load "AppConfig.fs"
#load "Settings.fs"
#load "Types.fs"
#load "Interactions.Core.fs"
#load "Interactions.fs"

#I "../../FsOpenAI.Vision"
#load "Image.fs"
#load "Video.fs"
#load "VisionApi.fs"

#I "../../FsOpenAI.GenAI"
#load "AsyncExts.fs"
#load "Env.fs"
#load "Connection.fs"
#load "Sessions.fs"
#load "Monitoring.fs"
#load "Gen/SemanticVectorSearch.fs"
#load "Gen/StreamParser.fs"
#load "Gen/TemplateParser.fs"
#load "Gen/Models.fs"
#load "Gen/Tokens.fs"
#load "Gen/ChatUtils.fs"
#load "Gen/Endpoints.fs"
#load "Gen/SKernel.fs"
#load "Gen/GenUtils.fs"
#load "Gen/Prompts.fs"
#load "Gen/Indexes.fs"
#load "Gen/Completions.fs"
#load "Gen/WebCompletion.fs"
#load "Gen/IndexQnA.fs"
#load "Gen/DocQnA.fs"
