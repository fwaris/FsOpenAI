module FsOpenAI.GenAI.SKernel
open System
open System.Collections.Generic
open System.Text.RegularExpressions
open FsOpenAI.Shared

type PromptExecutionSettings =
    {
        mutable MaxTokens : int
        mutable ResponseFormat : Type option
    }

type KernelArguments =
    {
        Values : IReadOnlyDictionary<string, string>
        Settings : PromptExecutionSettings
    }

[<RequireQualifiedAccess>]
module SKernel =
    let private buildValues (args:(string * string) seq) =
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        for (key, value) in args do
            values[key] <- if isNull value then String.Empty else value
        values :> IReadOnlyDictionary<string, string>

    let kernelArgsFrom (_:ServiceSettings) (ch:Interaction) (args:(string*string) seq) =
        {
            Values = buildValues args
            Settings = { MaxTokens = ch.Parameters.MaxTokens; ResponseFormat = None }
        }

    let kernelArgsDefault (args:(string*string) seq) =
        {
            Values = buildValues args
            Settings = { MaxTokens = 150; ResponseFormat = None }
        }

    let kernelArgs (args:(string*string) seq) (overrides:PromptExecutionSettings->unit) =
        let args = kernelArgsDefault args
        overrides args.Settings
        args

    let private restoreLiteralBraces (prompt:string) =
        prompt
            .Replace("{{ '{{' }}", "{{")
            .Replace("{{ '}}' }}", "}}")

    let renderPrompt (prompt:string) (args:KernelArguments) =
        task {
            let rendered =
                Regex.Replace(
                    restoreLiteralBraces prompt,
                    @"\{\{\s*\$([A-Za-z0-9_]+)\s*\}\}",
                    MatchEvaluator(fun m ->
                        match args.Values.TryGetValue(m.Groups.[1].Value) with
                        | true, value -> value
                        | _ -> String.Empty))
            return rendered
        }
