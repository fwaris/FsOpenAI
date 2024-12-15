namespace FsOpenAI.Shared.Interactions.CodeEval
open System
open FsOpenAI.Shared

module Interaction =
    let codeBag (ch:Interaction) = ch.Types |> List.tryPick (function CodeEval c -> Some c | _ -> None) 
  
    let setCode c ch =
        match codeBag ch with
        | Some _ -> {ch with Types = ch.Types |> List.map (function CodeEval bag -> CodeEval {bag with Code = c} | x -> x)}
        | None ->  {ch with Types = (CodeEval {CodeEvalBag.Default with Code=c})::ch.Types}

    let setPlan p ch =
        match codeBag ch with 
        | Some _ -> {ch with Types = ch.Types |> List.map (function CodeEval bag -> CodeEval {bag with Plan = p} | x -> x)}
        | None -> {ch with Types = (CodeEval {CodeEvalBag.Default with Plan=p})::ch.Types}

    let setEvalParms p ch =
        match codeBag ch with 
        | Some _ -> {ch with Types = ch.Types |> List.map (function CodeEval bag -> CodeEval {bag with CodeEvalParms = p} | x -> x)}
        | None -> {ch with Types = (CodeEval {CodeEvalBag.Default with CodeEvalParms=p})::ch.Types}
    

module Interactions =
    open FsOpenAI.Shared.Interactions.Core.Interactions

    let setCode id c cs = updateWith (Interaction.setCode c) id cs
    let setPlan id p cs = updateWith (Interaction.setPlan p) id cs
    let setEvalParms id p cs = updateWith (Interaction.setEvalParms p) id cs

module CodeEvalPrompts = 

    ///GPT models are good at F# code generation but stumble on F# string interpolation syntax.
    ///These instructions provide guidance on string interpolation as well general code generation
    let sampleCodeGenPrompt = """
# String Formatting:
- Use F# string interpolation in generated code, instead of the sprintf function.
- Only format number as percentages if number is a percentage.
- Remember in F# interpolated strings are prefixed with $.
- Do not use $ in the string interpolation.
- Remember to escape %% in the string interpolation for percentage formatting with %%%%.

# Code generation:
- The main function return signature should be of type string
- It is the answer to the user query
- In addition to user QUERY follow any PLAN and/or F# Type descriptions, if provided, to generate the code.
- Only generate new code. Do not output any existing code
- Do not create F# modules - use existing types and functions.
- Always generate functions with curried arguments.
- To create a set consider the 'set' operator instead of Set.ofXXX functions.
- Ensure that code formatting (whitespace, etc.) is correct according to F# conventions.
- Declare all constants at the top before the main function and then reference them in the code later
- Do not use F# reserved keywords as variable names (e.g. 'end', 'as', etc.).
- Put type annotations where needed so types are clear in the generated code.
- Prefer using |> when working with lists (e.g. invoices |> List.map ...) so the types are better inferred.
- When creating lists always use put the '[' on a new line, properly indented.
- ALWAYS TYPE ANNOTATE THE LAMBDA FUNCTION PARAMETERS.
- BE SURE TO ACTUALLY INVOKE THE GENERATED FUNCTION WITH THE APPROPRIATE ARGUMENTS TO RETURN THE FINAL RESULT.
- ALWAYS PRINT THE FINAL RESULT TO THE CONSOLE
"""

    let regenPromptTemplate = """
While compiling the given [F# CODE TO FIX] a compilation ERROR was encountered. Regenerate the code after fixing the error.
Only fix the error. Do not change the code structure. The line number of the error may not be accurate. Refer to
F# Types to fix code namely to properly type annotate the lambda function parameters.

ERROR```
{{{{$errorMessage}}}}
```

[F# CODE TO FIX]```
{{{{$code}}}}
```

# Other Instructions
FOLLOW F# WHITESPACE RULES
ENSURE THAT THE F# CODE RETURNS A VALUE OF TYPE string. Fix the code if necessary.

```fsharp

"""