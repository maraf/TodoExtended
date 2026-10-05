using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Components.AI;
using Microsoft.Extensions.AI;
using AiMessage = Microsoft.Extensions.AI.ChatMessage;

namespace TodoExtended.Web.Services.AiChat;

public sealed class BrowserConversationThread : IConversationThread
{
    private static readonly JsonSerializerOptions LegacyJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly List<ChatResponseUpdate> _updates = [];
    private readonly TimeProvider _clock;
    private readonly Dictionary<(ChatRole? Role, string Id), DateTimeOffset> _timestamps = [];
    private readonly ConditionalWeakTable<AIContent, MessageMetadata> _contentMetadata = new();

    public BrowserConversationThread(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public string ThreadId { get; } = Guid.NewGuid().ToString("N");
    public bool IsStateful => false;
    public string? ConversationId => null;
    public Action? OnTurnCompleted { get; set; }

    public void AppendUserMessage(AiMessage message)
    {
        // UIAgent copies the user's contents, but not the mapped update's ID or creation time.
        var metadata = message.Contents.Select(content =>
            _contentMetadata.TryGetValue(content, out var value) ? value : null).FirstOrDefault(value => value is not null);
        AppendUpdate(new ChatResponseUpdate
        {
            Role = message.Role,
            Contents = [.. message.Contents],
            MessageId = metadata?.Id ?? message.MessageId,
            CreatedAt = metadata?.Timestamp,
        });
    }

    internal void RecordTimestamp(ChatResponseUpdate update)
    {
        update.MessageId ??= Guid.NewGuid().ToString("N");
        var key = (update.Role, update.MessageId);
        if (!_timestamps.TryGetValue(key, out var timestamp))
        {
            timestamp = update.CreatedAt ?? _clock.GetUtcNow();
            _timestamps.Add(key, timestamp);
        }
        update.CreatedAt = timestamp;
        foreach (var content in update.Contents)
        {
            _contentMetadata.Remove(content);
            _contentMetadata.Add(content, new MessageMetadata(update.MessageId, timestamp));
        }
    }

    public DateTimeOffset GetTimestamp(RichContentBlock block) =>
        _timestamps.TryGetValue((block.Role, block.Id), out var timestamp)
            ? timestamp
            : throw new InvalidOperationException($"Chat block '{block.Id}' has no creation timestamp.");

    public void AppendUpdate(ChatResponseUpdate update)
    {
        RecordTimestamp(update);
        if (_updates.LastOrDefault() is { } previous &&
            previous.MessageId is not null && previous.MessageId == update.MessageId &&
            previous.Role == update.Role &&
            previous.Contents.Count == 1 && previous.Contents[0] is TextContent previousText &&
            update.Contents.Count == 1 && update.Contents[0] is TextContent text)
        {
            _updates[^1] = new ChatResponseUpdate(update.Role, previousText.Text + text.Text)
            {
                MessageId = update.MessageId,
                CreatedAt = previous.CreatedAt,
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
        _timestamps.Clear();
        _contentMetadata.Clear();
        // Restored proposals are display-only, even if approval was pending when the tab closed.
        foreach (var update in updates.TakeLast(2000))
        {
            foreach (var call in update.Contents.OfType<FunctionCallContent>())
                call.InformationalOnly = true;
            // Older update histories have no send time; assign it once at ingestion, never at render.
            AppendUpdate(update);
        }
    }

    public void LoadLegacy(string json)
    {
        var entries = JsonSerializer.Deserialize<List<LegacyEntry>>(json, LegacyJsonOptions)
            ?? throw new JsonException("Chat history is empty.");
        _updates.Clear();
        _timestamps.Clear();
        _contentMetadata.Clear();
        foreach (var entry in entries.TakeLast(200))
        {
            if (entry.Message is null)
                continue;
            var text = entry.Message.Text ?? "";
            if (entry.Message.TaskListReferences is { Count: > 0 } lists)
                text += "\n\n" + TaskAgentChatClient.FormatListLinks(lists);
            AppendUpdate(new ChatResponseUpdate(
                entry.Message.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                text) { CreatedAt = entry.Message.Timestamp });
            for (var i = 0; i < (entry.Message.ProposedActions?.Count ?? 0); i++)
            {
                var id = Guid.NewGuid().ToString("N");
                AppendUpdate(new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    CreatedAt = entry.Message.Timestamp,
                    Contents = [new FunctionCallContent(id, TaskAgentChatClient.ReviewToolName,
                        new Dictionary<string, object?>
                        {
                            ["proposalId"] = id,
                            ["action"] = JsonSerializer.SerializeToElement(entry.Message.ProposedActions![i]),
                        }) { InformationalOnly = true }],
                });
                if (entry.Results?.FirstOrDefault(result => result.ActionIndex == i) is { } result)
                    AppendUpdate(new ChatResponseUpdate
                    {
                        Role = ChatRole.Tool,
                        CreatedAt = entry.Message.Timestamp,
                        Contents = [new FunctionResultContent(id, result)],
                    });
            }
        }
    }

    private sealed record LegacyEntry(ChatMessage? Message, IReadOnlyList<ActionResult>? Results);
    private sealed record MessageMetadata(string Id, DateTimeOffset Timestamp);
}
