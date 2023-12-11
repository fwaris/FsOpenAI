#r "nuget: FSharp.Data"
open System
open System.IO
open FSharp.Data

type Q = CsvProvider< @"C:\s\gc\questions.csv">

let qs = Q.GetSample().Rows |> Seq.toList 
let questions = qs |> List.map(fun x->x.``Q #``)

let rec groupQ accQ acc (xs:Q.Row list) = 
    match xs with 
    | [] -> ((List.rev accQ)::acc) |> List.rev
    | x::rest when ((x.``Q #``).HasValue) -> groupQ [x] (List.rev accQ :: acc) rest
    | x::rest                             -> groupQ (x::accQ) acc rest

let grps = groupQ [] [] qs |> List.filter (List.isEmpty>>not)

let assembleQuestion (xs:Q.Row list) =
    let qn = (xs.[0].``Q #``).Value
    let q = xs.[0].Questions
    let ans = xs.[0].Answer
    let choices = xs |> List.map(fun r -> r.Column3, r.``Multiple Choice Answers, Select the most accurate answer``)
    qn,q,ans,choices

let qas = grps |> List.map assembleQuestion

let formulateQuestion (qn,q,ans,choices) =
   let choices = String.Join("; ", choices |> List.map(fun (a,b) -> $"{a}: {b}"))
   let qext = $"{q}. Choose your answer from the following choices: {choices}"
   qext

qas |> List.map formulateQuestion |> List.iter (printfn "%s")




