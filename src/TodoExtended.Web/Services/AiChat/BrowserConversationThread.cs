using System.Text.Json;
using Microsoft.AspNetCore.Components.AI;
using Microsoft.Extensions.AI;
using AiMessage = Microsoft.Extensions.AI.ChatMessage;

namespace TodoExtended.Web.Services.AiChat;

public sealed class BrowserConversationThread : IConversationThread
{
    private static readonly JsonSerializerOptions LegacyJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly List<ChatResponseUpdate> _updates = [];
    public string ThreadId { get; } = Guid.NewGuid().ToString("N");
    public bool IsStateful => false;
    public string? ConversationId => null;
    public Action? OnTurnCompleted { get; set; }

    public void AppendUserMessage(AiMessage message) =>
        _updates.Add(new ChatResponseUpdate { Role = message.Role, Contents = [.. message.Contents] });

    public void AppendUpdate(ChatResponseUpdate update)
    {
        if (_updates.LastOrDefault() is { } previous &&
            previous.MessageId is not null && previous.MessageId == update.MessageId &&
            previous.Role == update.Role &&
            previous.Contents.Count == 1 && previous.Contents[0] is TextContent previousText &&
            update.Contents.Count == 1 && update.Contents[0] is TextContent text)
        {
            _updates[^1] = new ChatResponseUpdate(update.Role, previousText.Text + text.Text)
            {
                MessageId = update.MessageId,
            };
        }
        else
        {
            _updates.Add(update);
        }
    }

    public void CompleteTurn()
    {
        var userIndexes = _updates.Select((update, index) => (update, index))
            .Where(item => item.update.Role == ChatRole.User)
            .Select(item => item.index).ToList();
        if (userIndexes.Count > 200)
            _updates.RemoveRange(0, userIndexes[^200]);
        OnTurnCompleted?.Invoke();
    }

    public IReadOnlyList<ChatResponseUpdate> GetUpdates() => _updates;

    public string Serialize() => JsonSerializer.Serialize(_updates);

    public void Load(string json)
    {
        var updates = JsonSerializer.Deserialize<List<ChatResponseUpdate>>(json)
            ?? throw new JsonException("Chat history is empty.");
        _updates.Clear();
        // Restored proposals are display-only, even if approval was pending when the tab closed.
        foreach (var update in updates.TakeLast(2000))
        {
            foreach (var call in update.Contents.OfType<FunctionCallContent>())
                call.InformationalOnly = true;
            _updates.Add(update);
        }
    }

    public void LoadLegacy(string json)
    {
        var entries = JsonSerializer.Deserialize<List<LegacyEntry>>(json, LegacyJsonOptions)
            ?? throw new JsonException("Chat history is empty.");
        _updates.Clear();
        foreach (var entry in entries.TakeLast(200))
        {
            if (entry.Message is null)
                continue;
            var text = entry.Message.Text ?? "";
            if (entry.Message.TaskListReferences is { Count: > 0 } lists)
                text += "\n\n" + TaskAgentChatClient.FormatListLinks(lists);
            _updates.Add(new ChatResponseUpdate(
                entry.Message.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                text));
            for (var i = 0; i < (entry.Message.ProposedActions?.Count ?? 0); i++)
            {
                var id = Guid.NewGuid().ToString("N");
                _updates.Add(new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new FunctionCallContent(id, TaskAgentChatClient.ReviewToolName,
                        new Dictionary<string, object?>
                        {
                            ["proposalId"] = id,
                            ["action"] = JsonSerializer.SerializeToElement(entry.Message.ProposedActions![i]),
                        }) { InformationalOnly = true }],
                });
                if (entry.Results?.FirstOrDefault(result => result.ActionIndex == i) is { } result)
                    _updates.Add(new ChatResponseUpdate
                    {
                        Role = ChatRole.Tool,
                        Contents = [new FunctionResultContent(id, result)],
                    });
            }
        }
    }

    private sealed record LegacyEntry(ChatMessage? Message, IReadOnlyList<ActionResult>? Results);
}
