namespace FsOpenAI.CodeEvaluator
open System

module Prompts =

    ///GPT models are good at F# code generation but stumble on F# string interpolation syntax.
    ///These instructions provide guidance on string interpolation as well general code generation
    let sampleCodeGenPrompt = """
# String Formatting:
- Use F# string interpolation in generated code, instead of the sprintf function.
- Only format number as percentagets if number is a percentage.
- Remember in F# interpolated strings are prefixed with $.
- Do not use $ in the string interpolation.
- Remember to escape %% in the string interpolation for percentage formatting with %%%%.

# Code generation:
- The main function return signature should be of type string
- It is the answer to the user query
- In addition to user QUERY follow the PLAN and F# Type descriptions to generate the code.
- Only generate new code. Do not output any existing code
- To get a list of invoices, use the value Data.invoices.
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
- JUST RETURN THE FINAL RESULT. DO NOT PRINT THE RESULT. DO NOT ASSIGN THE RESULT TO A VARIABLE.
"""

    ///Creates a SK prompt template to fix generated code.
    /// The fsTypes parameter is a string representation of F# types; the context under which
    /// the generated code is expected to be compilable
    let fixCodePrompt fsTypes = $"""
While compiling the given [F# CODE TO FIX] a compilation ERROR was encountered. Regenerate the code after fixing the error.
Only fix the error. Do not change the code structure. The line number of the error may not be accurate. Refer to
F# Types to fix code namely to properly type annotate the lambda function parameters.

F# Types```
{fsTypes}
```
````
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
