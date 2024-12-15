#r "nuget: FSharp.Compiler.Service"
open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax


module CodeScrub =
    let MAX_CODE_LEN = 300 * 80

    let splitCode (s:string) =
        let lines =
            seq {
                use string = new StringReader(s)
                let mutable line = string.ReadLine()
                while line <> null do
                    yield line
                    line <- string.ReadLine()
            }
            |> Seq.toList

        let interactiveLines =
            lines
            |> Seq.takeWhile(fun x -> not (x.Trim().StartsWith("let")))
            |> String.concat "\n"

        let expressionLines =
            lines
            |> Seq.skipWhile(fun x -> not (x.Trim().StartsWith("let")))
            |> String.concat "\n"

        interactiveLines,expressionLines

    let rec declNamespaces acc d = 
        match d with 
        | FSharpImplementationFileDeclaration.Entity (e, subDecls) ->
            let acc = Set.add e.Namespace acc
            (acc,subDecls) ||> List.fold declNamespaces
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, vs, e) ->
            if v.FullType.HasTypeDefinition then 
                Set.add v.FullType.TypeDefinition.Namespace acc
            else 
                Set.add (Some v.FullType.BasicQualifiedName) acc
        | FSharpImplementationFileDeclaration.InitAction(e) ->
            if e.Type.HasTypeDefinition then
                Set.add e.Type.TypeDefinition.Namespace acc
            else 
                Set.add (Some e.Type.BasicQualifiedName) acc

    let rec expressions acc d = 
        match d with 
        | FSharpImplementationFileDeclaration.Entity (e, subDecls) ->
            (acc,subDecls) ||> List.fold expressions
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, vs, e) -> e::acc
        | FSharpImplementationFileDeclaration.InitAction(e) -> acc

    let tast (input:string) = 
        let checker = FSharpChecker.Create(keepAssemblyContents=true)
        let file = Path.ChangeExtension(Path.GetTempFileName(), "fsx")  
        File.WriteAllText(file, input)
        let projOptions, _errors = 
            checker.GetProjectOptionsFromScript(file, SourceText.ofString input, assumeDotNetFramework=false)
            |> Async.RunSynchronously
        let results = checker.ParseAndCheckProject(projOptions) |> Async.RunSynchronously
        let checkedFile = results.AssemblyContents.ImplementationFiles.[0]
        try File.Delete file with _ -> ()
        checkedFile
    
    //Use typed AST to find all referenced namespaces      
    let namespaces (tast:FSharpImplementationFileContents) =
        let ns = (Set.empty,tast.Declarations) ||> List.fold declNamespaces
        ns |> Set.remove (Some "Microsoft.FSharp.Core") |> Seq.toList |> List.choose id |> set
   
    let bannedKeywords = ["nuget"; "#r"; "#load"]
    let containsBannedWords (code:string) =
        bannedKeywords |> List.exists (fun w -> code.Contains(w,StringComparison.OrdinalIgnoreCase))
        
    let allowedToExec allowedNamespaces preamble generatedCode =
        if containsBannedWords generatedCode then
            false
        elif generatedCode.Length > MAX_CODE_LEN then
            false
        else
            let interactiveLines,expressionLines = splitCode generatedCode
            let code = [preamble; interactiveLines; expressionLines] |> String.concat "\n"
            let tast = tast code
            let declNs = namespaces tast
            let extraNs = Set.difference declNs allowedNamespaces
            extraNs.Count = 0

let rec visitExpr f fT (e:FSharpExpr) = 
    f e
    match e with 
    | FSharpExprPatterns.AddressOf(lvalueExpr) -> 
        visitExpr f fT lvalueExpr
    | FSharpExprPatterns.AddressSet(lvalueExpr, rvalueExpr) -> 
        visitExpr f fT lvalueExpr; visitExpr f fT rvalueExpr
    | FSharpExprPatterns.Application(funcExpr, typeArgs, argExprs) -> 
        visitExpr f fT funcExpr; visitExprs f fT argExprs
    | FSharpExprPatterns.Call(objExprOpt, memberOrFunc, typeArgs1, typeArgs2, argExprs) -> 
        visitTypes f fT typeArgs1
        visitTypes f fT typeArgs2
        visitObjArg f fT objExprOpt; visitExprs f fT argExprs
    | FSharpExprPatterns.Coerce(targetType, inpExpr) -> 
        visitExpr f fT inpExpr
    | FSharpExprPatterns.FastIntegerForLoop(startExpr, limitExpr, consumeExpr, isUp, _, _) -> 
        visitExpr f fT startExpr; visitExpr f fT limitExpr; visitExpr f fT consumeExpr
    | FSharpExprPatterns.ILAsm(asmCode, typeArgs, argExprs) -> 
        visitTypes f fT typeArgs
        visitExprs f fT argExprs
    | FSharpExprPatterns.ILFieldGet (objExprOpt, fieldType, fieldName) -> 
        visitType f fT fieldType
        visitObjArg f fT objExprOpt
    | FSharpExprPatterns.ILFieldSet (objExprOpt, fieldType, fieldName, valueExpr) -> 
        visitType f fT fieldType
        visitExpr f fT valueExpr
        visitObjArg f fT objExprOpt
    | FSharpExprPatterns.IfThenElse (guardExpr, thenExpr, elseExpr) -> 
        visitExpr f fT guardExpr; visitExpr f fT thenExpr; visitExpr f fT elseExpr
    | FSharpExprPatterns.Lambda(lambdaVar, bodyExpr) -> 
        visitExpr f fT bodyExpr
    | FSharpExprPatterns.Let((bindingVar, bindingExpr, dbg), bodyExpr) -> 
        visitExpr f fT bindingExpr; visitExpr f fT bodyExpr
    | FSharpExprPatterns.LetRec(recursiveBindings, bodyExpr) ->
        for _,bindingExpr,_ in recursiveBindings do visitExpr f fT bindingExpr
        visitExpr f fT bodyExpr
    | FSharpExprPatterns.NewArray(arrayType, argExprs) -> 
        visitType f fT arrayType
        visitExprs f fT argExprs
    | FSharpExprPatterns.NewDelegate(delegateType, delegateBodyExpr) -> 
        visitType f fT delegateType
        visitExpr f fT delegateBodyExpr
    | FSharpExprPatterns.NewObject(objType, typeArgs, argExprs) -> 
        visitTypes f fT typeArgs
        visitExprs f fT argExprs
    | FSharpExprPatterns.NewRecord(recordType, argExprs) ->  
        visitType f fT recordType
        visitExprs f fT argExprs
    | FSharpExprPatterns.NewAnonRecord(recordType, argExprs) ->  
        visitType f fT recordType
        visitExprs f fT argExprs
    | FSharpExprPatterns.NewTuple(tupleType, argExprs) -> 
        visitType f fT tupleType
        visitExprs f fT argExprs
    | FSharpExprPatterns.NewUnionCase(unionType, unionCase, argExprs) -> 
        visitType f fT unionType
        visitExprs f fT argExprs
    | FSharpExprPatterns.Quote(quotedExpr) -> 
        visitExpr f fT quotedExpr
    | FSharpExprPatterns.FSharpFieldGet(objExprOpt, recordOrClassType, fieldInfo) -> 
        visitType f fT recordOrClassType
        visitObjArg f fT objExprOpt
    | FSharpExprPatterns.AnonRecordGet(objExpr, recordOrClassType, fieldInfo) -> 
        visitType f fT recordOrClassType
        visitExpr f fT objExpr
    | FSharpExprPatterns.FSharpFieldSet(objExprOpt, recordOrClassType, fieldInfo, argExpr) -> 
        visitType f fT recordOrClassType
        visitObjArg f fT objExprOpt; visitExpr f fT argExpr
    | FSharpExprPatterns.Sequential(firstExpr, secondExpr) -> 
        visitExpr f fT firstExpr; visitExpr f fT secondExpr
    | FSharpExprPatterns.TryFinally(bodyExpr, finalizeExpr, dbgTry, dbgFinally) -> 
        visitExpr f fT bodyExpr; visitExpr f fT finalizeExpr
    | FSharpExprPatterns.TryWith(bodyExpr, _, _, catchVar, catchExpr, dbgTry, dbgWith) -> 
        visitExpr f fT bodyExpr; visitExpr f fT catchExpr
    | FSharpExprPatterns.TupleGet(tupleType, tupleElemIndex, tupleExpr) -> 
        visitType f fT tupleType
        visitExpr f fT tupleExpr
    | FSharpExprPatterns.DecisionTree(decisionExpr, decisionTargets) -> 
        visitExpr f fT decisionExpr; List.iter (snd >> visitExpr f fT) decisionTargets
    | FSharpExprPatterns.DecisionTreeSuccess (decisionTargetIdx, decisionTargetExprs) -> 
        visitExprs f fT decisionTargetExprs
    | FSharpExprPatterns.TypeLambda(genericParam, bodyExpr) -> 
        visitExpr f fT bodyExpr
    | FSharpExprPatterns.TypeTest(ty, inpExpr) -> 
        visitType f fT ty
        visitExpr f fT inpExpr
    | FSharpExprPatterns.UnionCaseSet(unionExpr, unionType, unionCase, unionCaseField, valueExpr) -> 
        visitType f fT unionType        
        visitExpr f fT unionExpr; visitExpr f fT valueExpr
    | FSharpExprPatterns.UnionCaseGet(unionExpr, unionType, unionCase, unionCaseField) -> 
        visitType f fT unionType        
        visitExpr f fT unionExpr
    | FSharpExprPatterns.UnionCaseTest(unionExpr, unionType, unionCase) -> 
        visitType f fT unionType        
        visitExpr f fT unionExpr
    | FSharpExprPatterns.UnionCaseTag(unionExpr, unionType) -> 
        visitType f fT unionType        
        visitExpr f fT unionExpr
    | FSharpExprPatterns.ObjectExpr(objType, baseCallExpr, overrides, interfaceImplementations) -> 
        visitType f fT objType
        visitExpr f fT baseCallExpr
        List.iter (visitObjMember f fT) overrides
        List.iter (snd >> List.iter (visitObjMember f fT)) interfaceImplementations
    | FSharpExprPatterns.TraitCall(sourceTypes, traitName, typeArgs, typeInstantiation, argTypes, argExprs) -> 
        visitTypes f fT sourceTypes
        visitTypes f fT typeInstantiation
        visitTypes f fT argTypes
        visitExprs f fT argExprs
    | FSharpExprPatterns.ValueSet(valToSet, valueExpr) -> 
        visitExpr f fT valueExpr
    | FSharpExprPatterns.WhileLoop(guardExpr, bodyExpr, dbg) -> 
        visitExpr f fT guardExpr; visitExpr f fT bodyExpr
    | FSharpExprPatterns.BaseValue baseType -> fT baseType
    | FSharpExprPatterns.DefaultValue defaultType -> fT defaultType
    | FSharpExprPatterns.ThisValue thisType -> fT thisType
    | FSharpExprPatterns.Const(constValueObj, constType) -> fT constType
    | FSharpExprPatterns.Value(valueToGet) -> ()
    | _ -> failwith (sprintf "unrecognized %+A" e)

and visitExprs f fT exprs = 
    List.iter (visitExpr f fT) exprs

and visitObjArg f fT objOpt = 
    Option.iter (visitExpr f fT) objOpt

and visitObjMember f fT memb = 
    visitExpr f fT memb.Body

and visitType f fT t = fT t
    
and visitTypes f fT ts = 
    ts |> List.iter (visitType f fT)

let testCode = """

open System.Net

let downloadFile (url: string) : string =
    use client = new WebClient()
    client.DownloadString(url)

downloadFile "http://www.google.com"
"""

let t2  = CodeScrub.tast testCode
let exps = ([],t2.Declarations) ||> List.fold CodeScrub.expressions
let ts : Ref<Set<string>> = ref Set.empty 
let f (e:FSharpExpr) = ts.Value <- Set.add e.Type.BasicQualifiedName ts.Value 
let fT (e:FSharpType) = ts.Value <- Set.add e.BasicQualifiedName ts.Value 
exps |> List.iter (visitExpr f fT)
ts.Value

let rec fibonacci (n: int): int =
    if n <= 1 then n
    else fibonacci (n - 1) + fibonacci (n - 2)

let fib100 = fibonacci 30

