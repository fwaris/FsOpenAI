namespace FsOpenAI.Client.Interactions
open System
open FSharp.Control
open FsOpenAI.Client
open FSharp.Reflection

module Interaction = 
    let newUserMessage cntnt = {MsgId = Utils.newId(); Role=MessageRole.User; Message=cntnt}
    let newAsstantMessage cntnt =  {MsgId = Utils.newId(); Role=MessageRole.Assistant QueriedDocuments.Empty; Message=cntnt}

    let getText c = 
        let sb = System.Text.StringBuilder()
        sb.Append("[System]").AppendLine().AppendLine(c.SystemMessage) |> ignore
        c.Messages 
        |> List.iter (fun m -> 
            let tag = if m.IsUser then "[User]" else "[Assistant]"
            sb.Append(tag).AppendLine().AppendLine(m.Message) |> ignore)
        sb.ToString()

    let genName name msg question = 
        match Utils.isEmpty msg, Utils.isEmpty question with
        | true,true   -> name
        | true,false  -> Utils.shorten 14 question
        | _,_         -> Utils.shorten 14 msg

    let tag (ch:Interaction) =
        let cType = match ch.InteractionType with Chat _ -> "Chat" | QA _ -> "Q&A" | DocQA _ -> "Doc. "
        $"{cType} [{ch.Parameters.Backend}] ..."

    let systemMessage (c:Interaction) = c.SystemMessage

    let messages (c:Interaction) = c.Messages 

    let cBag c =
        match c.InteractionType with
        | Chat b -> b
        | _ -> failwith "unexpected interaction type"

    let configuredChatBackends (cfg:AppConfig) = 
        (cfg.ModelsConfig.LongChatModels |> List.map _.Backend)
        @ (cfg.ModelsConfig.ShortChatModels |> List.map _.Backend)
        |> List.distinct

    let getModeCases() = FSharpType.GetUnionCases typeof<ExplorationMode>
    let getModeCase (mode:ExplorationMode) = FSharpValue.GetUnionFields(mode,typeof<ExplorationMode>)
        
    let maxDocs defaultVal (ch:Interaction) = 
        match ch.InteractionType with 
        | QA bag -> bag.MaxDocs 
        | DocQA dbag -> dbag.QABag.MaxDocs
        | _ -> defaultVal

    let lastSearchQuery (ch:Interaction) = 
        List.rev ch.Messages 
        |> List.map (_.Role) 
        |> List.tryPick (function Assistant x -> x.SearchQuery | _ -> None)

    let lastNonEmptyUserMessageText (ch:Interaction) =
        let msg = ch.Messages |> List.rev |> List.tryFind(fun x->x.IsUser && Utils.notEmpty x.Message) 
        match msg with 
        | Some msg -> msg.Message
        | None     -> ""

    let name (ch:Interaction) =
        match ch.Name with 
        | Some n -> n
        | None   -> genName (tag ch) (lastNonEmptyUserMessageText ch) ch.Question

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

    let setIndexes idxs c = 
        match c.InteractionType with 
        | QA bag -> {c with InteractionType = QA {bag with Indexes = idxs}}
        | DocQA dbag -> {c with InteractionType = DocQA {dbag with QABag = {dbag.QABag with Indexes = idxs}}}
        | _ -> failwith "unexpected chat type"

    let clearDocuments c = 
        {c with 
            Messages = 
                c.Messages 
                |> List.map(fun m -> 
                    match m.Role with 
                    | Assistant r -> {m with Role = Assistant QueriedDocuments.Empty} 
                    | _           -> m)
        }

    let addDocuments docs c = 
        let h,tail = match List.rev c.Messages with h::t -> h,t | _ -> failwith "no messages in chat"
        let h = 
            match h.Role with 
            | Assistant d -> {h with Role = Assistant {d with Docs=docs}}
            | _ -> failwith "Expected an Assistant message"
        let msgs = h::tail
        let _,msgs = 
            ((0,[]),msgs) 
            ||> List.fold (fun (count,acc) m -> 
                let count',m' = 
                    match m.Role with 
                    | Assistant _ when count > C.MAX_DOCLISTS_PER_CHAT -> count+1,{m with Role = Assistant QueriedDocuments.Empty}
                    | Assistant _ -> count+1,m
                    | _ -> count,m
                count',m'::acc)
        {c with Messages = msgs}

    let addDelta delta c = 
        let h,tail = match List.rev c.Messages with h::t -> h,t | _ -> failwith "no messages in chat"
        let h = {h with Message = h.Message+delta} 
        {c with Messages = List.rev (h::tail)}

    let setSystemMessage msg (c:Interaction) = {c with SystemMessage = msg}

    let setQuestion q (c:Interaction) = {c with Question = q}   

    let setMode mode ch = {ch with Parameters = {ch.Parameters with Mode = mode}}

    let setMaxDocs maxDocs c = 
        match c.InteractionType with
        | QA bag -> {c with InteractionType = QA {bag with MaxDocs = maxDocs }}
        | DocQA dbag -> {c with InteractionType = DocQA {dbag with QABag = {dbag.QABag with MaxDocs = maxDocs}}}
        | _          -> c 

    let endBuffering errOccured c  = 
        let c = {c with IsBuffering=false}
        if errOccured then
            let msgs = List.rev c.Messages  
            match msgs with 
            | t1::rest when t1.IsUser    -> {c with Messages = List.rev rest; Question=t1.Message.Trim()}
            | _::t1::rest when t1.IsUser -> {c with Messages = List.rev rest; Question=t1.Message.Trim()}
            | _ -> failwith "unexpected chat state"
        else    
            c

    let setUserMessage msg c =
        let msgs = List.rev c.Messages
        let msgs' = 
            match msgs with
            | []                        -> [newUserMessage msg]
            | t1::rest when t1.IsUser   -> {t1 with Message=msg}::rest
            | xs                        -> (newUserMessage msg)::xs                        
        {c with Messages = List.rev msgs'}

    let restartFromMsg (msg:InteractionMessage) (c:Interaction) = 
        if c.IsBuffering then 
            c
        elif not msg.IsUser then failwith "cannot restart chat from non-user message"
        else
            let msgs = c.Messages |> List.takeWhile (fun msg' -> msg<>msg') 
            {c with Messages = msgs; Question = msg.Message }                                 

    let clearChat prompt (c:Interaction) = {c with Messages = []; Question=prompt}

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
        | Some q -> setQuestion q ch
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

    let isReady ch = 
        not ch.IsBuffering &&
        match ch.InteractionType with 
        | DocQA dbag -> dbag.Document.Status = Ready
        | _ -> true
        
    
    let defaultParameters backend interactionType = 
        {InteractionParameters.Default with 
            Backend = backend
            Mode = 
                match interactionType with 
                | QA _ | DocQA _  -> ExplorationMode.Factual
                | _     -> InteractionParameters.Default.Mode
        }

    ///trim UI state that is not required for processing chat
    let removeUIState ch =
        
        match ch.InteractionType with
        | DocQA dbag -> {ch with InteractionType = DocQA {dbag with Document = {dbag.Document with DocumentRef=None;}}}
        | _ -> ch

    //trim state that is not required for processing chat
    let preSerialize ch =
        ch
        |> removeUIState
        |> clearDocuments

    let create ctype backend msg =        
        let iType = 
            match ctype with 
            | CreateChat      -> InteractionType.Chat ChatBag.Default
            | CreateQA        -> InteractionType.QA QABag.Default
            | CreateDocQA lbl -> InteractionType.DocQA {DocBag.Default with Label=lbl}
        let msg = defaultArg msg ""
        let id = Utils.newId()
        let c = 
            {
                Id = id
                Name = None
                InteractionType = iType
                SystemMessage = C.defaultSystemMessage
                Question = msg
                Messages = []
                Parameters = defaultParameters backend iType
                Timestamp = DateTime.Now
                IsBuffering = false
                Notifications = []
            }
        c.Id,c

    let setUseWeb useWeb c = 
        match c.InteractionType with 
        | Chat cbag -> {c with InteractionType = Chat {UseWeb=useWeb}}
        | _ -> failwith "unexpected chat type"

    let toggleDocOnly c = 
        match c.InteractionType with 
        | DocQA dbag -> {c with InteractionType = DocQA {dbag with DocOnlyQuery = not dbag.DocOnlyQuery}}
        | _ -> failwith "unexpected chat type"

module Interactions =

    let empty = []

    let addNew backend ctype msg cs = 
        let id,c = Interaction.create ctype backend msg
        id, cs @ [c]

    let private update f id c = if c.Id = id then f c else c
    let private updateWith f id  cs = cs |> List.map(update f id)

    let remove id cs = cs |> List.filter(fun c -> c.Id <> id)

    let setQABag id bag cs = updateWith (Interaction.setQABag bag) id cs
    
    let setDocBag id dbag cs = updateWith (Interaction.setDocBag dbag) id cs

    let setUserMessage id msg cs = updateWith (Interaction.setUserMessage msg) id cs

    let setLastUserMessage id msg cs = updateWith (Interaction.setUserMessage msg) id cs

    let addMessage id msg cs = updateWith (fun c -> {c with Messages = c.Messages @ [msg]}) id cs

    let clearChat id prompt cs = updateWith (Interaction.clearChat prompt) id cs
    
    let restartFromMsg id msg cs =  updateWith (Interaction.restartFromMsg msg) id cs

    let setSystemMessage id sysMsg cs = updateWith (Interaction.setSystemMessage sysMsg) id cs
 
    let setName id name cs = updateWith (fun c -> {c with Name=name}) id cs

    let setParms id parms cs = updateWith (fun c -> {c with Parameters=parms}) id cs

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

    let setQuestion id q cs = updateWith (Interaction.setQuestion q) id cs

    let addDocuments id docs cs = updateWith (Interaction.addDocuments docs) id cs 

    let setMaxDocs id maxDocs cs = updateWith (Interaction.setMaxDocs maxDocs) id cs

    let setIndexes id idxs cs = updateWith (Interaction.setIndexes idxs) id cs

    let setMode id mode cs = updateWith (Interaction.setMode mode) id cs

    let toggleDocOnly id cs = updateWith (Interaction.toggleDocOnly) id cs
