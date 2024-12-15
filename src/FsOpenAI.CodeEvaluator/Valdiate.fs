namespace FsOpenAI.CodeEvaluator
open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols

type ParseCheck = ParseError of string | ParseChecked of FSharpImplementationFileContents
type CodeCheck = CodeCheck_Error of string | CodeCheck_Pass | ChodeCheck_Denied

module Validate =
    let MAX_CODE_LEN = 300 * 80
    let MAX_CODE_EVAL_TIME_MS = 20_000 // 20 seconds
    let MAX_REGEN_ATTEMPTS = 2 //number of attempts to correct code after compile errors
    let MAX_EVAL_PARALLELISM = 2 //number of concurrent evaluation sessions for the machine

    /// Return preamble code and the set of allowed namespaces, for the given key.
    /// Preamble code is allowed to contain #r references to support generated code (generated code is not allowed #r)
    let preamble key = "",set ["System"]  //by default only allow types from System namespace

    let rec expressions acc d = 
        match d with 
        | FSharpImplementationFileDeclaration.Entity (e, subDecls) ->
            (acc,subDecls) ||> List.fold expressions
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, vs, e) -> e::acc
        | FSharpImplementationFileDeclaration.InitAction(e) -> acc

    ///typed AST of the given code
    let tast (input:string) = 
        let checker = FSharpChecker.Create(keepAssemblyContents=true)
        let file = Path.ChangeExtension(Path.GetTempFileName(), "fsx")  
        File.WriteAllText(file, input)
        let projOptions, _errors = 
            checker.GetProjectOptionsFromScript(file, SourceText.ofString input, assumeDotNetFramework=false)
            |> Async.RunSynchronously
        let results = checker.ParseAndCheckProject(projOptions) |> Async.RunSynchronously
        try File.Delete file with _ -> ()
        let errors =
            results.Diagnostics 
            |> Array.filter (fun x -> x.Severity = FSharp.Compiler.Diagnostics.FSharpDiagnosticSeverity.Error)
            |> Array.map (fun x -> x.Message)
        let errMsg = if errors.Length = 0 then None else Some (String.concat "\n" errors)
        match errMsg with 
        | None -> ParseChecked results.AssemblyContents.ImplementationFiles.[0]
        | Some msg -> ParseError msg

    ///visit each node in the AST with the supplied functions f (for visited expression) and fT (for visited type)
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

    let rec addType acc (t:FSharpType) = 
        if t.GenericArguments.Count = 0 then
            Set.add t.BasicQualifiedName acc 
        else 
            let acc = t.BaseType |> Option.map (addType acc) |> Option.defaultValue acc
            (acc,t.GenericArguments) ||> Seq.fold (addType)

    //Use typed AST to find all referenced namespaces      
    let namespaces (tast:FSharpImplementationFileContents) =
        let exps = ([],tast.Declarations) ||> List.fold expressions
        let ts : Ref<Set<string>> = ref Set.empty 
        let f (e:FSharpExpr) = ts.Value <- addType ts.Value e.Type
        let fT (e:FSharpType) = ts.Value <- addType ts.Value e
        exps |> List.iter (visitExpr f fT)
        let nss = ts.Value |> Set.map (fun t -> let i = t.LastIndexOf(".") in if i > 0 then t.Substring(0,i) else t)
        nss |> Set.remove "Microsoft.FSharp.Core"

    let bannedKeywords = ["nuget"; "#r"; "#load"]
    let containsBannedWords (code:string) =
        bannedKeywords |> List.exists (fun w -> code.Contains(w,StringComparison.OrdinalIgnoreCase))
        
    let allowedToExec allowedNamespaces preamble generatedCode =
        if containsBannedWords generatedCode then
            ChodeCheck_Denied
        elif generatedCode.Length > MAX_CODE_LEN then
            ChodeCheck_Denied
        else            
            let code = [preamble; generatedCode] |> String.concat "\n"
            match tast code with 
            | ParseError msg -> CodeCheck_Error msg
            | ParseChecked tast -> 
                let declNs = namespaces tast
                let extraNs = Set.difference declNs allowedNamespaces
                if extraNs.Count = 0 then CodeCheck_Pass else ChodeCheck_Denied
