namespace FsOpenAI.Client.Interactions
open System
open FSharp.Control
open FsOpenAI.Client

module Interaction = 
    let newUserMessage cntnt = {Role=MessageRole.User Open; Message=cntnt}
    let newAsstantMessage cntnt =  {Role=MessageRole.Assistant; Message=cntnt}

    let genName name msg = if Utils.isEmpty msg then name else (msg |>Seq.truncate 14 |> Seq.toArray |> String)

    let updateQABag bag c = 
        match c.InteractionType with 
        | QA _ -> {c with InteractionType = QA bag}
        | _    -> c

    let clearDocuments c =
        match c.InteractionType with
        | QA bag -> {c with InteractionType = QA {bag with Documents=[]}}
        | _      -> c

    let updateLastMsgWith f c = 
        let h,tail = match List.rev c.Messages with h::t -> h,t | _ -> failwith "no messages in chat"
        let h = f h 
        {c with Messages = List.rev (h::tail)}

    let updateSystemMsg msg c = 
        match c.InteractionType with
        | Chat _ -> {c with InteractionType = Chat msg }
        | QA bag -> {c with InteractionType = QA {bag with SystemMessage = msg }}

    let endBuffering errOccured c  = 
        let msgs = List.rev c.Messages  
        let msgs =
            if errOccured then 
                msgs |> List.skipWhile(fun m -> Utils.isEmpty m.Message)
            else
                msgs
        let msgs =
            match msgs with 
            | [] -> [newUserMessage ""]
            | h::rest when h.IsUser -> {h with Role=User Open}::rest
            | xs                    -> (newUserMessage "")::xs
        {c with Messages=List.rev msgs; IsBuffering=false}            

    let addOrUpdateLastMsg msg c = 
        let (h,t) = match List.rev c.Messages with h::t -> {h with Message=msg},t | _ -> (newUserMessage msg),[]
        match h.Role with | MessageRole.User _ -> () | _ -> failwith "user role expected"
        let h = {h with Role=MessageRole.User Closed}
        {c with Messages = List.rev (h::t); Name=genName c.Name h.Message}

    let tryDeleteMessage msg (c:Interaction) = 
        if c.IsBuffering then 
            c
        else 
            let c = {c with Messages = c.Messages |> List.takeWhile (fun msg' -> msg<>msg')}
            let msgs = match c.Messages with [] -> [newUserMessage ""] | x::[] -> [newUserMessage x.Message] | xs -> xs
            {c with Messages = msgs}                

    let chatParameters backend interactionType = 
        {InteractionParameters.Default with 
            Backend = backend
            Temperature = 
                match interactionType with 
                | QA _  -> 0.1 //default to low for improved precision
                | _     -> InteractionParameters.Default.Temperature
        }

    let defaultName n bkend = 
        let n = n |> Seq.truncate 3 |> Seq.toArray |> String
        match bkend with
        | CreateChat AzureOpenAI -> $"Chat [Azure] {n}"
        | CreateQA AzureOpenAI -> $"Q&A [Azure] {n}"
        | CreateChat OpenAI -> $"Chat [OpenAI] {n}"
        | CreateQA OpenAI -> $"Q&A [OpenAI] {n}"

    let addDelta delta c = updateLastMsgWith (fun m -> {m with Message=m.Message+delta}) c

    let systemMessage c = match c.InteractionType with Chat s -> s | QA bag -> bag.SystemMessage

    let getModels (sp:ServiceSettings option) (ch:Interaction) (f:ModelDeployments->string list) =
        match ch.Parameters.Backend with
        | Backend.AzureOpenAI -> sp |> Option.bind(fun sp -> sp.AZURE_OPENAI_MODELS |> Option.map f) |> Option.defaultValue []
        | Backend.OpenAI -> sp |> Option.bind(fun sp -> sp.OPENAI_MODELS |> Option.map f) |> Option.defaultValue []

    let chatModels (sp:ServiceSettings option) (ch:Interaction) =
        let models = getModels sp ch (fun x->x.CHAT)
        models |> List.map(fun m -> m, (ch.Parameters.ChatModel=m))

    let completionsModels (sp:ServiceSettings option) (ch:Interaction) =
        let models = getModels sp ch (fun x->x.COMPLETION)
        models |> List.map(fun m -> m, (ch.Parameters.CompletionsModel=m))

    let embeddingsModel (sp:ServiceSettings option) (ch:Interaction) =
        let models = getModels sp ch (fun x->x.EMBEDDING)
        models |> List.map(fun m -> m, (ch.Parameters.EmbeddingsModel=m))

    let maxDocs defaultVal (ch:Interaction) = match ch.InteractionType with QA bag -> bag.MaxDocs | _ -> defaultVal

module Interactions =

    let empty = []

    let addNew ctype msg cs = 
        let iType,bknd = 
            match ctype with 
            | CreateChat bk -> InteractionType.Chat Prompts.defaultSystemMessage, bk
            | CreateQA bk -> InteractionType.QA QABag.Default, bk
        let msg = defaultArg msg ""
        let id = Utils.newId()
        let name = Interaction.genName (Interaction.defaultName id ctype) msg
        let c = 
            {
                Id = id
                Name = name
                InteractionType = iType
                Messages = [Interaction.newUserMessage msg]
                Parameters = Interaction.chatParameters bknd iType
                Timestamp = DateTime.Now
                IsBuffering = false
                Notifications = []
            }
        c.Id,cs @ [c]

    let private update f id c = if c.Id = id then f c else c
    let private updateWith f id  cs = cs |> List.map(update f id)

    let remove id cs = cs |> List.filter(fun c -> c.Id <> id)

    let updateQABag id bag cs = updateWith (Interaction.updateQABag bag) id cs

    let addOrUpdateLastMsg (id,msg) cs = updateWith (Interaction.addOrUpdateLastMsg msg) id cs

    let addMessage (id,msg) cs = updateWith (fun c -> {c with Messages = c.Messages @ [msg]}) id cs
    
    let tryDeleteMessage (id,msg) cs =  updateWith (Interaction.tryDeleteMessage msg) id cs

    let updateSystemMsg (id,sysMsg) cs = updateWith (Interaction.updateSystemMsg sysMsg) id cs
 
    let updateName (id,name) cs = updateWith (fun c -> {c with Name=name}) id cs

    let updateParms (id,parms) cs = updateWith (fun c -> {c with Parameters=parms}) id cs

    let addDelta id delta cs = updateWith (Interaction.addDelta delta) id cs

    let startBuffering id cs = updateWith (fun c -> {c with IsBuffering=true}) id cs
    
    let endBuffering id errorOccured cs = updateWith (Interaction.endBuffering errorOccured) id cs 
 
    let updateNotification id note cs = updateWith (fun c -> {c with Notifications=c.Notifications @ [note]}) id cs 

    let clearNotifications id cs = updateWith (fun c -> {c with Notifications=[]}) id cs

    let clearDocuments id cs = updateWith Interaction.clearDocuments id cs
