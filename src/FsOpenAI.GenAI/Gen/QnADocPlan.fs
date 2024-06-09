namespace FsOpenAI.GenAI
open FsOpenAI.Shared

module QnADocPlan =

    let runPlan (parms:ServiceSettings) modelsConfig (ch:Interaction) dispatch =
        async {  
            try
                do! DocQnA.runDocOnlyPlan parms modelsConfig ch dispatch
            with ex -> 
                dispatch (Srv_Ia_Done(ch.Id, Some ex.Message))
        }
