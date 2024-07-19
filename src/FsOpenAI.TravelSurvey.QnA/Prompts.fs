namespace FsOpenAI.TravelSurvey

module Prompts = 

    let planSysMessage nhtsTypes = $"""
You are an AI assistant that understands a modified NHTS dataset.

The complete F# types decribing all aspects of the NHTS dataset are as follows:
```
{nhtsTypes}
```

# Goal:
Your goal is to analyze a user query and either create a step-by-step plan of action that can be executed to answer the query or ask a clarifying question if the query is unclear. Try to make reasonable assumptions about the question. Only ask the user if absolutely necessary.
If you decide to generate plan then respond with 'plan:' as the first word otherwise respond with 'query:'.

# Other Instructions:
- Be sure about your analysis. Don't make things up. And think step-by-step before generating. 
- Don't include any Python code in the response
- Most questions may be answerable from a single type, choose the most related type to answer the query.

"""

    let planPrompt
        question
        = $"""
Query```
{question}
```
"""

    let codeSysMessage plan = """
You are an AI assistant that can generate F# code to answer a user QUERY by leveraging a step-by-step PLAN to guide the code generation.
# Main function:
- The main function return signature should be of type: string that answers the user query.
# Plan:
{plan}
"""

    let helperFunctions = """
- FsOpenAI.TravelSurvey.Types.Helpers.formatNumber: Format a number to a string with two decimal places
- FsOpenAI.TravelSurvey.Types.Helpers.formatNumberPercent: Format a number to a string as a percentage with two decimal places
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
- Do not create F# modules. Use existing types and function where possible.
- Always generate functions with curried arguments.
- To create a set consider the 'set' operator instead of Set.ofXXX functions.
- Ensure that code formatting (whitespace, etc.) is correct according to F# conventions.
- Declare all constants at the top before the main function and then reference them in the code later
- Do not use F# reserved keywords as variable names (e.g. 'end', 'as', etc.).
- Put type annotations where needed so types are clear in the generated code.
- Prefer using |> when working with lists (e.g. data_list |> List.map ...) so the types are better inferred.
- Refer to [F# Types] to ensure that Union Cases are correctly geneated so the code is compilable.
- Use fully qualifield Union Cases when pattern matching and in Boolean expressions, e.g. TRAVDAY.TRAVDAY_Saturday instead of just TRAVDAY_Saturday; MAKE.MAKE_Ford instead of Make_Ford, etc.
- Be mindful when refering to union cases that contain spaces and other special characters. Ensure that they are encased in double backticks (`) in generated code.
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
F# Types```
{fsTypes}
```
Helper Functions```
{helperFunctions}
```

{codeGenInstructions}

```fsharp

"""
