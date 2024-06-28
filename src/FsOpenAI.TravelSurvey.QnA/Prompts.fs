namespace FsOpenAI.TravelSurvey

module Prompts = 

    let planSysMessage nhtsTypes = $"""
You are an AI assistant that understands a modified NHTS dataset. The dataset is described by:
{nhtsTypes}

# Goal:
Your goal is to analyze a user query and either create a step-by-step plan of action that can be executed to answer the query or ask a clarifying question if the query is unclear.
If you decide to generate plan then respond with 'plan:' as the first word otherwise respond with 'query:'.

# Other Instructions:
Be sure about your analysis. Don't make things up. And think step-by-step before generating. Don't include any Python code in the response
"""

    let planPrompt
        question
        = $"""
Query```
{question}
```
"""

    let codeSysMessage = """
You are an AI assistant that can generate F# code to answer a user QUERY on a modified NHTS dataset and a step-by-step plan to guide the code generation.
# Main function:
- The main function return signature should be of type: string that answers the user query.
"""

    let helperFunctions = """
- Helpers.formatNumber: Format a number to a string with two decimal places
- Helpers.formatNumberPercent: Format a number to a string as a percentage with two decimal places
- Data.load() : Lazy<FsOpenAI.TravelSurvey.Types.DataSets> : Load the NHTS dataset
 """

    let codeGenInstructions = """
# String Formatting:
- Use F# string interpolation in generated code, instead of the sprintf function.
- ALWAYS USE THE FUNCTIONS Helpers.formatNumber and Helpers.formatNumberPercentage to format numbers.
- Only format number as percentage if number is a percentage value.
- Remember in F# interpolated strings are prefixed with $.
- Do not use $ in the string interpolation.
- Remember to escape %% in the string interpolation for percentage formatting with %%%%.

# Code generation:
- In addition to user QUERY follow the PLAN and F# Type descriptions to generate the code.
- Only generate new code. Do not output any existing code
- Do not create F# modules - use existing types and functions.
- Always generate functions with curried arguments.
- To create a set consider the 'set' operator instead of Set.ofXXX functions.
- Ensure that code formatting (whitespace, etc.) is correct according to F# conventions.
- Declare all constants at the top before the main function and then reference them in the code later
- Do not use F# reserved keywords as variable names (e.g. 'end', 'as', etc.).
- Put type annotations where needed so types are clear in the generated code.
- Prefer using |> when working with lists (e.g. data_list |> List.map ...) so the types are better inferred.
- When creating lists always use put the '[' on a new line, properly indented.
- ALWAYS TYPE ANNOTATE THE LAMBDA FUNCTION PARAMETERS.
- BE SURE TO ACTUALLY INVOKE THE GENERATED FUNCTION WITH THE APPROPRIATE ARGUMENTS TO RETURN THE FINAL RESULT.
- JUST RETURN THE FINAL RESULT. DO NOT PRINT THE RESULT. DO NOT ASSIGN THE RESULT TO A VARIABLE.
"""

    let codePrompt
        question
        plan
        fsTypes
        = $"""
QUERY```
{question}
```

PLAN```
{plan}
```

F# Types```
{fsTypes}
```

Helper Functions```
{helperFunctions}
```

{codeGenInstructions}

```fsharp

"""
