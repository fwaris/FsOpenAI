module FsOpenAI.GenAI.ChatUtils
open FsOpenAI.Shared
open Microsoft.SemanticKernel.ChatCompletion

[<RequireQualifiedAccess>]
module ChatUtils = 

    let temperature = function
        | Factual -> 0.f
        | Exploratory -> 0.2f
        | Creative -> 0.7f

    let serializeChat (ch:Interaction) : ChatLog =
        {
            SystemMessge = ch.SystemMessage
            Messages =
                ch.Messages
                |> Seq.filter (fun m -> not(Utils.isEmpty m.Message))
                |> Seq.map(fun m -> {ChatLogMsg.Role = (match m.Role with User -> "User" | _ -> "Assistant"); ChatLogMsg.Content = m.Message})
                |> Seq.toList
            Temperature = ch.Parameters.Mode |> temperature |> float
            MaxTokens = ch.Parameters.MaxTokens
        }

    let toChatHistory (ch:Interaction) =
        let h = ChatHistory()
        if ch.Parameters.ModelType <> MT_Logic  && Utils.notEmpty ch.SystemMessage then //o1 does not support system messages
            h.AddSystemMessage(ch.SystemMessage)
        for m in ch.Messages do
            let role = if m.IsUser then AuthorRole.User else AuthorRole.Assistant
            h.AddMessage(role,m.Message)
        h


