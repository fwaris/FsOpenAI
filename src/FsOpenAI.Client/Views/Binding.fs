/// Two-way binding for HTML input elements.
/// credit: https://github.com/fsbolero/Bolero/issues/189
module Bind
open Bolero
open System
open System.Globalization
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Forms
open Microsoft.FSharp.Linq.RuntimeHelpers

/// <exclude />
[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
let inline binder< ^T, ^B, ^O when ^B : (static member FormatValue : ^T * CultureInfo -> ^O)>
        (valueAttribute: string) (value: ^T) (callback: ^T -> unit) cultureInfo =
    Attr(fun receiver builder sequence ->
        builder.AddAttribute(sequence, valueAttribute, (^B : (static member FormatValue : ^T * CultureInfo -> ^O)(value, cultureInfo)))
        builder.AddAttribute(sequence + 1, valueAttribute + "Changed", EventCallback.Factory.Create(receiver, Action<'T>(callback)))
        sequence + 2)

[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
let inline binderThreeState< ^T, ^B, ^O when ^B : (static member FormatValue : ^T * CultureInfo -> ^O)>
        (valueAttribute: string) (value: ^T) (callback: ^T -> unit) cultureInfo =
    Attr(fun receiver builder sequence ->
        builder.AddAttribute(sequence, "ThreeState", true)
        builder.AddAttribute(sequence + 1, "CheckState", (^B : (static member FormatValue : ^T * CultureInfo -> ^O)(value, cultureInfo)))
        builder.AddAttribute(sequence + 2, "CheckStateChanged", EventCallback.Factory.Create(receiver, Action<'T>(callback)))
        sequence + 3)

[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
let inline binderFieldId< ^T, ^B, ^O when ^B : (static member FormatValue : ^T * CultureInfo -> ^O)>
        (valueAttribute: string) (value: ^T) (callback: ^T -> unit) (fieldIdentifier: FieldIdentifier) cultureInfo =
    Attr(fun receiver builder sequence ->
        builder.AddAttribute(sequence, "FieldIdentifier", fieldIdentifier)
        builder.AddAttribute(sequence + 1, valueAttribute, (^B : (static member FormatValue : ^T * CultureInfo -> ^O)(value, cultureInfo)))
        builder.AddAttribute(sequence + 2, valueAttribute + "Changed", EventCallback.Factory.Create(receiver, Action<'T>(callback)))
        sequence + 3)

[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
let inline binderThreeStateFieldId< ^T, ^B, ^O when ^B : (static member FormatValue : ^T * CultureInfo -> ^O)>
        (valueAttribute: string) (value: ^T) (callback: ^T -> unit) (fieldIdentifier: FieldIdentifier) cultureInfo =
    Attr(fun receiver builder sequence ->
        builder.AddAttribute(sequence, "ThreeState", true)
        builder.AddAttribute(sequence + 1, "FieldIdentifier", fieldIdentifier)
        builder.AddAttribute(sequence + 2, "CheckState", (^B : (static member FormatValue : ^T * CultureInfo -> ^O)(value, cultureInfo)))
        builder.AddAttribute(sequence + 3, "CheckStateChanged", EventCallback.Factory.Create(receiver, Action<'T>(callback)))
        sequence + 4)

[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
let inline binderExpression< ^T, ^B, ^O when ^B : (static member FormatValue : ^T * CultureInfo -> ^O)>
        (valueAttribute: string) (valueExpression: Quotations.Expr<Func< ^T>>) (callback: ^T -> unit) cultureInfo =
    let valueExpression = valueExpression |> LeafExpressionConverter.QuotationToLambdaExpression
    let valueFunction = valueExpression.Compile()
    Attr(fun receiver builder sequence ->
        let value = valueFunction.Invoke()
        builder.AddAttribute(sequence, valueAttribute, (^B : (static member FormatValue : ^T * CultureInfo -> ^O)(value, cultureInfo)))
        builder.AddAttribute(sequence + 1, valueAttribute + "Changed", EventCallback.Factory.Create(receiver, Action<'T>(callback)))
        builder.AddAttribute(sequence + 2, valueAttribute + "Expression", valueExpression)
        sequence + 3)

[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
let inline binderExpressionRaw< ^T, ^B, ^O when ^B : (static member FormatValue : ^T * CultureInfo -> ^O)>
        (valueAttribute: string) (valueExpression: Quotations.Expr<Func< ^T>>) (callback: ^T -> unit) cultureInfo =
    let valueExpression = valueExpression |> LeafExpressionConverter.QuotationToLambdaExpression
    let valueFunction = valueExpression.Compile()
    Attr(fun receiver builder sequence ->
        let value = valueFunction.Invoke()
        builder.AddAttribute(sequence, valueAttribute, value)
        builder.AddAttribute(sequence + 1, valueAttribute + "Changed", EventCallback.Factory.Create(receiver, Action<'T>(callback)))
        builder.AddAttribute(sequence + 2, valueAttribute + "Expression", valueExpression)
        sequence + 3)

[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
let inline binderThreeStateExpression< ^T, ^B, ^O when ^B : (static member FormatValue : ^T * CultureInfo -> ^O)>
        (valueAttribute: string) (valueExpression: Quotations.Expr<Func< ^T>>) (callback: ^T -> unit) (fieldIdentifier: FieldIdentifier) cultureInfo =
    let valueExpression = valueExpression |> LeafExpressionConverter.QuotationToLambdaExpression
    let valueFunction = valueExpression.Compile()
    Attr(fun receiver builder sequence ->
        let value = valueFunction.Invoke()
        builder.AddAttribute(sequence, "ThreeState", true)
        builder.AddAttribute(sequence + 1, "CheckState", (^B : (static member FormatValue : ^T * CultureInfo -> ^O)(value, cultureInfo)))
        builder.AddAttribute(sequence + 2, "CheckStateChanged", EventCallback.Factory.Create(receiver, Action<'T>(callback)))
        builder.AddAttribute(sequence + 2, valueAttribute + "Expression", valueExpression)
        sequence + 4)

/// <summary>Bind a boolean to the value of a checkbox with 3 states.</summary>
/// <param name="value">The current checked state.</param>
/// <param name="callback">The function called when the checked state changes.</param>
let inline CheckState value callback = binderThreeState<bool Nullable, BindConverter, bool Nullable> "Value" value callback null

/// <summary>Bind a boolean to the value of a checkbox with 3 states.</summary>
/// <param name="value">The current checked state.</param>
/// <param name="callback">The function called when the checked state changes.</param>
let inline CheckStateFieldId value callback fieldIdentifier = binderThreeStateFieldId<bool Nullable, BindConverter, bool Nullable> "Value" value callback fieldIdentifier null

/// <summary>Bind a boolean to the value of a checkbox with 3 states.</summary>
/// <param name="value">The current checked state.</param>
/// <param name="callback">The function called when the checked state changes.</param>
let inline CheckStateExpressoin value callback fieldIdentifier = binderThreeStateExpression<bool Nullable, BindConverter, bool Nullable> "Value" value callback fieldIdentifier null

/// <summary>
/// Bind to the Value of an input. The Value is updated on the <c>oninput</c> event.
/// </summary>
[<RequireQualifiedAccess>]
module Input =

    /// <summary>
    /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline bool value callback = binder<bool, BindConverter, bool> "Value" value callback null

    /// <summary>
    /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline string value callback = binder<string, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind an integer to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline int value callback = binder<int, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind an int64 to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline int64 value callback = binder<int64, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a float to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline float value callback = binder<float, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a float32 to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline float32 value callback = binder<float32, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a decimal to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline decimal value callback = binder<decimal, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a DateTime to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline dateTime value callback = binder<DateTime, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a DateTimeOffset to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline dateTimeOffset value callback = binder<DateTimeOffset, BindConverter, string> "Value" value callback null

/// <summary>
/// Bind to the Value of an input. The Value is updated on the <c>oninput</c> event.
/// </summary>
[<RequireQualifiedAccess>]
module InputFieldId =

    /// <summary>
    /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline bool value callback fieldIdentifier = binderFieldId<bool, BindConverter, bool> "Value" value callback fieldIdentifier null

    /// <summary>
    /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline string value callback fieldIdentifier = binderFieldId<string, BindConverter, string> "Value" value callback fieldIdentifier null

    /// <summary>
    /// Bind an integer to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline int value callback fieldIdentifier = binderFieldId<int, BindConverter, string> "Value" value callback fieldIdentifier null

    /// <summary>
    /// Bind an int64 to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline int64 value callback fieldIdentifier = binderFieldId<int64, BindConverter, string> "Value" value callback fieldIdentifier null

    /// <summary>
    /// Bind a float to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline float value callback fieldIdentifier = binderFieldId<float, BindConverter, string> "Value" value callback fieldIdentifier null

    /// <summary>
    /// Bind a float32 to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline float32 value callback fieldIdentifier = binderFieldId<float32, BindConverter, string> "Value" value callback fieldIdentifier null

    /// <summary>
    /// Bind a decimal to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline decimal value callback fieldIdentifier = binderFieldId<decimal, BindConverter, string> "Value" value callback fieldIdentifier null

    /// <summary>
    /// Bind a DateTime to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline dateTime value callback fieldIdentifier = binderFieldId<DateTime, BindConverter, string> "Value" value callback fieldIdentifier null

    /// <summary>
    /// Bind a DateTimeOffset to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline dateTimeOffset value callback fieldIdentifier = binderFieldId<DateTimeOffset, BindConverter, string> "Value" value callback fieldIdentifier null

/// <summary>
/// Bind to the Value of an input. The Value is updated on the <c>oninput</c> event.
/// </summary>
module InputExpression =

    /// <summary>
    /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline bool value callback = binderExpression<bool, BindConverter, bool> "Value" value callback null

    /// <summary>
    /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline string value callback = binderExpression<string, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind an integer to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline int value callback = binderExpression<int, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind an integer to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline intRaw value callback = binderExpressionRaw<int, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind an int64 to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline int64 value callback = binderExpression<int64, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a float to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline float value callback = binderExpression<float, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a float32 to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline float32 value callback = binderExpression<float32, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a decimal to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline decimal value callback = binderExpression<decimal, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a DateTime to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline dateTime value callback = binderExpression<DateTime, BindConverter, string> "Value" value callback null

    /// <summary>
    /// Bind a DateTimeOffset to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    /// <param name="Value">The current input state.</param>
    /// <param name="callback">The function called when the input state changes.</param>
    let inline dateTimeOffset value callback = binderExpression<DateTimeOffset, BindConverter, string> "Value" value callback null

/// <summary>
/// Bind to the Value of an input and convert using the given <see cref="T:System.Globalization.CultureInfo" />.
/// </summary>
module withCulture =

    /// <summary>Bind a boolean to the value of a checkbox with 3 states.</summary>
    /// <param name="culture">The culture to use to parse the Value.</param>
    /// <param name="value">The current checked state.</param>
    /// <param name="callback">The function called when the checked state changes.</param>
    let inline CheckState culture value callback = binderThreeState<bool Nullable, BindConverter, bool Nullable> "Value" value callback culture

    /// <summary>Bind a boolean to the value of a checkbox with 3 states.</summary>
    /// <param name="culture">The culture to use to parse the Value.</param>
    /// <param name="value">The current checked state.</param>
    /// <param name="callback">The function called when the checked state changes.</param>
    let inline CheckStateFiedlId culture value callback fieldIdentifier = binderThreeStateFieldId<bool Nullable, BindConverter, bool Nullable> "Value" value callback fieldIdentifier culture

    /// <summary>Bind a boolean to the value of a checkbox with 3 states.</summary>
    /// <param name="culture">The culture to use to parse the Value.</param>
    /// <param name="value">The current checked state.</param>
    /// <param name="callback">The function called when the checked state changes.</param>
    let inline CheckStateExpression culture value callback fieldIdentifier = binderThreeStateExpression<bool Nullable, BindConverter, bool Nullable> "Value" value callback fieldIdentifier culture

    /// <summary>
    /// Bind to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    [<RequireQualifiedAccess>]
    module Input =

        /// <summary>
        /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline bool culture value callback = binder<bool, BindConverter, bool> "Value" value callback culture

        /// <summary>
        /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline string culture value callback = binder<string, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind an integer to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline int culture value callback = binder<int, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind an int64 to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline int64 culture value callback = binder<int64, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a float to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline float culture value callback = binder<float, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a float32 to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline float32 culture value callback = binder<float32, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a decimal to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline decimal culture value callback = binder<decimal, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a DateTime to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline dateTime culture value callback = binder<DateTime, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a DateTimeOffset to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline dateTimeOffset culture value callback = binder<DateTimeOffset, BindConverter, string> "Value" value callback culture

    /// <summary>
    /// Bind to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    [<RequireQualifiedAccess>]
    module InputFieldId =

        /// <summary>
        /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline bool culture value callback fieldIdentifier = binderFieldId<bool, BindConverter, bool> "Value" value callback fieldIdentifier culture

        /// <summary>
        /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline string culture value callback fieldIdentifier = binderFieldId<string, BindConverter, string> "Value" value callback fieldIdentifier culture

        /// <summary>
        /// Bind an integer to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline int culture value callback fieldIdentifier = binderFieldId<int, BindConverter, string> "Value" value callback fieldIdentifier culture

        /// <summary>
        /// Bind an int64 to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline int64 culture value callback fieldIdentifier = binderFieldId<int64, BindConverter, string> "Value" value callback fieldIdentifier culture

        /// <summary>
        /// Bind a float to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline float culture value callback fieldIdentifier = binderFieldId<float, BindConverter, string> "Value" value callback fieldIdentifier culture

        /// <summary>
        /// Bind a float32 to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline float32 culture value callback fieldIdentifier = binderFieldId<float32, BindConverter, string> "Value" value callback fieldIdentifier culture

        /// <summary>
        /// Bind a decimal to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline decimal culture value callback fieldIdentifier = binderFieldId<decimal, BindConverter, string> "Value" value callback fieldIdentifier culture

        /// <summary>
        /// Bind a DateTime to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline dateTime culture value callback fieldIdentifier = binderFieldId<DateTime, BindConverter, string> "Value" value callback fieldIdentifier culture

        /// <summary>
        /// Bind a DateTimeOffset to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline dateTimeOffset culture value callback fieldIdentifier = binderFieldId<DateTimeOffset, BindConverter, string> "Value" value callback fieldIdentifier culture

    /// <summary>
    /// Bind to the Value of an input. The Value is updated on the <c>oninput</c> event.
    /// </summary>
    module InputExpression =

        /// <summary>
        /// Bind a string to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline string culture value callback = binderExpression<string, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind an integer to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline int culture value callback = binderExpression<int, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind an int64 to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline int64 culture value callback = binderExpression<int64, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a float to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline float culture value callback = binderExpression<float, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a float32 to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline float32 culture value callback = binderExpression<float32, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a decimal to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline decimal culture value callback = binderExpression<decimal, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a DateTime to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline dateTime culture value callback = binderExpression<DateTime, BindConverter, string> "Value" value callback culture

        /// <summary>
        /// Bind a DateTimeOffset to the Value of an input. The Value is updated on the <c>oninput</c> event.
        /// </summary>
        /// <param name="culture">The culture to use to parse the Value.</param>
        /// <param name="Value">The current input state.</param>
        /// <param name="callback">The function called when the input state changes.</param>
        let inline dateTimeOffset culture value callback = binderExpression<DateTimeOffset, BindConverter, string> "Value" value callback culture
    