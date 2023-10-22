namespace FsOpenAI.Client.Interactions
open System
open FSharp.Control
open FsOpenAI.Client

module Interaction = 
    let newUserMessage cntnt = {Role=MessageRole.User Open; Message=cntnt}
    let newAsstantMessage cntnt =  {Role=MessageRole.Assistant; Message=cntnt}

    let genName name msg = if Utils.isEmpty msg then name else msg.Substring(0,min 14 (msg.Length-1)) + "…"

    let tag (ch:Interaction) =
        let cType = match ch.InteractionType with Chat _ -> "Chat" | QA _ -> "Q&A" | DocQA _ -> "Doc. "
        $"{cType} [{ch.Parameters.Backend}] ..."

    let systemMessage c = 
        match c.InteractionType with 
        | Chat s -> s.SystemMessage
        | QA bag -> bag.SystemMessage 
        | DocQA dbag -> dbag.QABag.SystemMessage

    let cBag c =
        match c.InteractionType with
        | Chat b -> b
        | _ -> failwith "unexpected interaction type"

    let getModels (sp:ServiceSettings option) (ch:Interaction) (f:ModelDeployments->string list) =
        match ch.Parameters.Backend with
        | Backend.AzureOpenAI -> sp |> Option.bind(fun sp -> sp.AZURE_OPENAI_MODELS |> Option.map f) |> Option.defaultValue []
        | Backend.OpenAI -> sp |> Option.bind(fun sp -> sp.OPENAI_MODELS |> Option.map f) |> Option.defaultValue []

    let chatModels (sp:ServiceSettings option) (ch:Interaction) =
        let models = getModels sp ch (fun x->x.CHAT)
        models |> List.map(fun m -> m, (ch.Parameters.ChatModel=m))

    let getDocuments (ch:Interaction) =
        match ch.InteractionType with
        | QA bag -> bag.Documents
        | DocQA dbag -> dbag.QABag.Documents
        | Chat cbag -> cbag.Documents

    let completionsModels (sp:ServiceSettings option) (ch:Interaction) =
        let models = getModels sp ch (fun x->x.COMPLETION)
        models |> List.map(fun m -> m, (ch.Parameters.CompletionsModel=m))

    let embeddingsModel (sp:ServiceSettings option) (ch:Interaction) =
        let models = getModels sp ch (fun x->x.EMBEDDING)
        models |> List.map(fun m -> m, (ch.Parameters.EmbeddingsModel=m))

    let maxDocs defaultVal (ch:Interaction) = 
        match ch.InteractionType with 
        | QA bag -> bag.MaxDocs 
        | DocQA dbag -> dbag.QABag.MaxDocs
        | _ -> defaultVal

    let searchQuery (ch:Interaction) = 
        match ch.InteractionType with 
        | QA bag -> bag.SearchQuery
        | DocQA dbag -> dbag.QABag.SearchQuery
        | _ -> failwith "unexpected chat type"

    let lastNonEmptyUserMessageText (ch:Interaction) =
        let msg = ch.Messages |> List.rev |> List.tryFind(fun x->x.IsUser && Utils.notEmpty x.Message) 
        match msg with 
        | Some msg -> msg.Message
        | None     -> ""

    let name (ch:Interaction) =
        match ch.Name with 
        | Some n -> n
        | None   -> genName (tag ch) (lastNonEmptyUserMessageText ch)

    let docBag (ch:Interaction) = match ch.InteractionType with DocQA dbag -> dbag | _ -> failwith "unexpected chat type"

    let getPrompt tpType ch =
        let dbag  = docBag ch
        match tpType with
        | DocQuery -> dbag.QueryTemplate
        | Extraction -> dbag.ExtractTermsTemplate

    let canSubmit (ch:Interaction) =
        if ch.IsBuffering then
            false
        else
            match ch.InteractionType with
            | DocQA dbag -> dbag.Document.Status = Ready
            | _          -> true


    let qaBag ch = 
        match ch.InteractionType with 
        | QA bag     -> Some bag 
        | DocQA dbag -> Some dbag.QABag 
        | _          -> None

    let setQABag bag c = 
        match c.InteractionType with 
        | QA _ -> {c with InteractionType = QA bag}
        | DocQA dbag -> {c with InteractionType = DocQA {dbag with QABag = bag}}
        | _    -> failwith "unexpected chat type"

    let setDocBag dbag c = 
        match c.InteractionType with         
        | DocQA _ -> {c with InteractionType = DocQA dbag}
        | _    -> failwith "unexpected chat type"

    let clearDocuments c =
        match c.InteractionType with
        | QA bag -> {c with InteractionType = QA {bag with Documents=[]}}
        | DocQA dbag -> {c with InteractionType = DocQA {dbag with QABag = {dbag.QABag with Documents = []}}}
        | Chat cbag -> {c with InteractionType = Chat {cbag with Documents=[]}}

    let setDocuments docs c = 
        match c.InteractionType with
        | QA bag -> {c with InteractionType = QA {bag with Documents=docs}}
        | DocQA dbag -> {c with InteractionType = DocQA {dbag with QABag = {dbag.QABag with Documents = docs}}}
        | Chat cbag -> {c with InteractionType = Chat {cbag with Documents=docs}}


    let addDelta delta c = 
        let h,tail = match List.rev c.Messages with h::t -> h,t | _ -> failwith "no messages in chat"
        let h = {h with Message = h.Message+delta} 
        {c with Messages = List.rev (h::tail)}

    let setSystemMessage msg c = 
        match c.InteractionType with
        | Chat cbag -> {c with InteractionType = Chat {cbag with SystemMessage=msg }}
        | QA bag -> {c with InteractionType = QA {bag with SystemMessage = msg }}
        | DocQA dbag -> {c with InteractionType = DocQA {dbag with QABag = {dbag.QABag with SystemMessage = msg}}}

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

    let setUserMessage shouldClose msg c =
        let role = User  (if shouldClose then Closed else Open)
        let msgs = List.rev c.Messages
        match msgs with
        | [] -> {c with Messages = [{newUserMessage msg with Role=role}]}
        | t1::rest when t1.IsUser -> {c with Messages = List.rev ({Message=msg; Role=role}::rest)}
        | t1::t2::rest when t2.IsUser -> {c with Messages = List.rev (t1::{Message=msg; Role=role}::rest)}
        | _ -> failwith "Unexpected chat message sequence"       

    let updateAndCloseLastUserMsg msg c = setUserMessage true msg c

    let tryDeleteMessage msg (c:Interaction) = 
        if c.IsBuffering then 
            c
        else 
            let msgs = c.Messages |> List.takeWhile (fun msg' -> msg<>msg') |> List.rev
            let msgs =
                match msgs with 
                | [] -> [newUserMessage ""]
                | t::rest when t.IsUser -> {t with Role = User UserRoleStatus.Open}::rest
                | t::rest -> newUserMessage ""::t::rest
                |> List.rev
            {c with Messages = msgs}

    let setFileContents (text,isDone) (ch:Interaction) = 
        let dbag = match ch.InteractionType with DocQA dbag -> dbag | _ -> failwith "unexpected chat type"
        let cnts = dbag.Document.DocumentText
        let cnts = match cnts with Some t -> String.Join("\r",[t;text]) | None -> text
        let doc = {dbag.Document with DocumentText = Some cnts; Status=if isDone then Ready else dbag.Document.Status}
        {ch with InteractionType = DocQA {dbag with Document = doc}}

    let setDocumentStatus status (ch:Interaction) = 
        let dbag = match ch.InteractionType with DocQA dbag -> dbag | _ -> failwith "unexpected chat type"
        let doc = {dbag.Document with Status = status}      
        {ch with InteractionType = DocQA {dbag with Document = doc}}

    let applyTemplate (tpType,template) (ch:Interaction) =
        let dbag = docBag ch
        let dbag =
            match tpType with 
            | DocQuery -> {dbag with QueryTemplate = Some template.Template;}
            | Extraction -> {dbag with ExtractTermsTemplate = Some template.Template}
        let ch = setDocBag dbag ch
        match template.Question with 
        | Some q -> setUserMessage false q ch
        | None   -> ch

    let addIndex idx ch =
        let updateBag (bag:QABag) = {bag with Indexes = bag.Indexes @ [idx]}
        match ch.InteractionType with 
        | QA bag -> setQABag (updateBag bag) ch
        | DocQA dbag -> setDocBag {dbag with QABag = (updateBag dbag.QABag)} ch
        | _ -> failwith "give chat type does not contain indexes"

    let setPrompt (tpType,prompt) ch =
        let prompt = if Utils.isEmpty prompt then None else Some prompt
        let dbag = docBag ch 
        let dbag = 
            match tpType with 
            | DocQuery -> {dbag with QueryTemplate = prompt}
            | Extraction -> {dbag with ExtractTermsTemplate = prompt}
        setDocBag dbag ch

    let setParameters parms (ch:Interaction) = {ch with Parameters = parms}
        
    let defaultParameters backend interactionType = 
        {InteractionParameters.Default with 
            Backend = backend
            Temperature = 
                match interactionType with 
                | QA _ | DocQA _  -> 0.1 //default to low for improved precision
                | _     -> InteractionParameters.Default.Temperature
        }

    ///trim UI state that is not required for processing chat
    let preSubmit ch =
        match ch.InteractionType with
        | DocQA dbag -> {ch with InteractionType = DocQA {dbag with Document = {dbag.Document with DocumentRef=None;}}}
        | _ -> ch

    //trim UI state that should not be saved 
    let preSerialize ch =
        ch
        |> preSubmit
        |> clearDocuments

    let create ctype msg =        
        let iType,bknd = 
            match ctype with 
            | CreateChat bk -> InteractionType.Chat  ChatBag.Default, bk
            | CreateQA bk -> InteractionType.QA QABag.Default, bk
            | CreateDocQA (bk,lbl) -> InteractionType.DocQA {DocBag.Default with Label=lbl}, bk
        let msg = defaultArg msg ""
        let id = Utils.newId()
        let c = 
            {
                Id = id
                Name = None
                InteractionType = iType
                Messages = [newUserMessage msg]
                Parameters = defaultParameters bknd iType
                Timestamp = DateTime.Now
                IsBuffering = false
                Notifications = []
            }
        c.Id,c

    let setUseWeb useWeb c = 
        match c.InteractionType with 
        | Chat cbag -> {c with InteractionType = Chat {cbag with UseWeb=useWeb}}
        | _ -> failwith "unexpected chat type"

module Interactions =

    let empty = []

    let addNew ctype msg cs = 
        let id,c = Interaction.create ctype msg
        id, cs @ [c]

    let private update f id c = if c.Id = id then f c else c
    let private updateWith f id  cs = cs |> List.map(update f id)

    let remove id cs = cs |> List.filter(fun c -> c.Id <> id)

    let setQABag id bag cs = updateWith (Interaction.setQABag bag) id cs
    
    let setDocBag id dbag cs = updateWith (Interaction.setDocBag dbag) id cs

    let updateAndCloseLastUserMsg (id,msg) cs = updateWith (Interaction.updateAndCloseLastUserMsg msg) id cs

    let setLastUserMessage (id,msg) cs = updateWith (Interaction.setUserMessage false msg) id cs

    let addMessage (id,msg) cs = updateWith (fun c -> {c with Messages = c.Messages @ [msg]}) id cs
    
    let tryDeleteMessage (id,msg) cs =  updateWith (Interaction.tryDeleteMessage msg) id cs

    let setSystemMessage (id,sysMsg) cs = updateWith (Interaction.setSystemMessage sysMsg) id cs
 
    let setName (id,name) cs = updateWith (fun c -> {c with Name=name}) id cs

    let setParms (id,parms) cs = updateWith (fun c -> {c with Parameters=parms}) id cs

    let addDelta id delta cs = updateWith (Interaction.addDelta delta) id cs

    let startBuffering id cs = updateWith (fun c -> {c with IsBuffering=true}) id cs
    
    let endBuffering id errorOccured cs = updateWith (Interaction.endBuffering errorOccured) id cs 
 
    let addNotification id note cs = updateWith (fun c -> {c with Notifications=c.Notifications @ [note]}) id cs 

    let clearNotifications id cs = updateWith (fun c -> {c with Notifications=[]}) id cs

    let clearDocuments id cs = updateWith Interaction.clearDocuments id cs

    let setFileContents id (text,isDone) cs = updateWith (Interaction.setFileContents (text,isDone)) id cs

    let setDocumentStatus id status cs = updateWith (Interaction.setDocumentStatus status) id cs

    let applyTemplate id (tpType,template) cs = updateWith (Interaction.applyTemplate (tpType,template)) id cs

    let setPrompt id (tpType,prompt) cs = updateWith (Interaction.setPrompt (tpType,prompt)) id cs

    let setUseWeb id useWeb cs = updateWith (Interaction.setUseWeb useWeb) id cs

    let setDocuments id docs cs = updateWith (Interaction.setDocuments docs) id cs 
