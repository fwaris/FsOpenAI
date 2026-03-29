module FsOpenAI.GenAI.ChatUtils
open FsOpenAI.Shared
open Microsoft.Extensions.AI

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

    let toChatMessages (ch:Interaction) : ChatMessage list =
        let msgs = ResizeArray<ChatMessage>()
        if Utils.notEmpty ch.SystemMessage then
            msgs.Add(ChatMessage(ChatRole.System, ch.SystemMessage))
        for m in ch.Messages do
            if Utils.notEmpty m.Message then
                let role = if m.IsUser then ChatRole.User else ChatRole.Assistant
                msgs.Add(ChatMessage(role, m.Message))
        List.ofSeq msgs
