using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using AiMessage = Microsoft.Extensions.AI.ChatMessage;
using AiResponse = Microsoft.Extensions.AI.ChatResponse;

namespace TodoExtended.Web.Services.AiChat;

public sealed class TaskAgentChatClient(IChatService chatService) : IChatClient
{
    public const string ReviewToolName = "review_task_action";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, ProposedAction> _pending = new();
    private readonly object _reviewLock = new();
    private CancellationTokenSource _reviewCancellation = new();
    private bool _disposed;

    public bool IsPending(string proposalId) => _pending.ContainsKey(proposalId);

    public void ClearPending()
    {
        lock (_reviewLock)
        {
            if (_disposed) return;
            _pending.Clear();
            _reviewCancellation.Cancel();
            _reviewCancellation.Dispose();
            _reviewCancellation = new CancellationTokenSource();
        }
    }

    public async Task<ActionResult> ReviewActionAsync(
        string proposalId, bool? approved = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (approved is null)
            throw new InvalidOperationException("Review the proposed action before continuing.");
        ProposedAction action;
        CancellationTokenSource cts;
        lock (_reviewLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_pending.TryRemove(proposalId, out action!))
                throw new InvalidOperationException("This proposal is no longer pending. Ask for a new proposal.");
            if (!approved.Value)
                return new ActionResult(0, false, "Rejected. No changes made.");
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _reviewCancellation.Token);
        }
        using (cts)
        {
            var results = await chatService.ExecuteActionsAsync(
                [action], [new ActionConfirmation(0, true)], cts.Token);
            return results.Single();
        }
    }

    public async Task<AiResponse> GetResponseAsync(
        IEnumerable<AiMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        await GetStreamingResponseAsync(messages, options, cancellationToken)
            .ToChatResponseAsync(cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<AiMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        lock (_reviewLock)
            ObjectDisposedException.ThrowIf(_disposed, this);
        var conversation = messages.ToList();
        var latest = conversation.LastOrDefault()
            ?? throw new ArgumentException("A message is required.", nameof(messages));
        var messageId = Guid.NewGuid().ToString("N");

        if (latest.Role == ChatRole.Tool)
        {
            var summary = string.Join("\n", latest.Contents.OfType<FunctionResultContent>()
                .Select(result => ReadResult(result.Result).Message));
            yield return new ChatResponseUpdate(ChatRole.Assistant, summary) { MessageId = messageId };
            yield break;
        }

        if (latest.Role != ChatRole.User || string.IsNullOrWhiteSpace(latest.Text))
            throw new ArgumentException("A user message is required.", nameof(messages));
        ClearPending();

        // UIAgent already appends the current message. Keep it out of the prior history.
        var history = conversation.Take(conversation.Count - 1)
            .Select(message => new ChatMessage(
                message.Role == ChatRole.User ? "user" : "assistant",
                message.Role == ChatRole.Tool
                    ? string.Join("\n", message.Contents.OfType<FunctionResultContent>()
                        .Select(result => ReadResult(result.Result).Message))
                    : message.Text,
                null, DateTimeOffset.UtcNow))
            .ToList();

        var channel = Channel.CreateBounded<ChatResponseUpdate>(new BoundedChannelOptions(32)
        {
            SingleReader = true,
            SingleWriter = true,
        });
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var producer = ProduceAsync();
        try
        {
            await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken))
                yield return update;
            await producer;
        }
        finally
        {
            await cts.CancelAsync();
            await producer;
        }

        async Task ProduceAsync()
        {
            try
            {
                var response = await chatService.SendStreamingMessageAsync(
                    latest.Text, history,
                    text => channel.Writer.WriteAsync(
                        new ChatResponseUpdate(ChatRole.Assistant, text) { MessageId = messageId },
                        cts.Token).AsTask(), cts.Token);

                if (response.TaskListReferences is { Count: > 0 })
                {
                    await channel.Writer.WriteAsync(new ChatResponseUpdate
                    {
                        Role = ChatRole.Assistant,
                        MessageId = Guid.NewGuid().ToString("N"),
                        Contents = [new TextContent(FormatListLinks(response.TaskListReferences))],
                    }, cts.Token);
                }

                foreach (var action in response.ProposedActions)
                {
                    var id = Guid.NewGuid().ToString("N");
                    lock (_reviewLock)
                    {
                        ObjectDisposedException.ThrowIf(_disposed, this);
                        cts.Token.ThrowIfCancellationRequested();
                        _pending[id] = action;
                    }
                    await channel.Writer.WriteAsync(new ChatResponseUpdate
                    {
                        Role = ChatRole.Assistant,
                        MessageId = Guid.NewGuid().ToString("N"),
                        Contents = [new FunctionCallContent(id, ReviewToolName, new Dictionary<string, object?>
                        {
                            ["proposalId"] = id,
                            ["action"] = JsonSerializer.SerializeToElement(action),
                        })],
                    }, cts.Token);
                }
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                _pending.Clear();
                channel.Writer.TryComplete(ex);
            }
        }
    }

    public static ProposedAction ReadAction(IDictionary<string, object?>? arguments)
    {
        if (arguments?.TryGetValue("action", out var value) == true &&
            value is JsonElement element &&
            element.Deserialize<ProposedAction>(JsonOptions) is { } action)
            return action;
        throw new InvalidOperationException("The proposal does not contain a valid task action.");
    }

    public static ActionResult ReadResult(object? value) => value switch
    {
        ActionResult result => result,
        JsonElement element => element.Deserialize<ActionResult>(JsonOptions)
            ?? throw new InvalidOperationException("Invalid task action result."),
        _ => throw new InvalidOperationException("Invalid task action result."),
    };

    public static string FormatListLinks(IReadOnlyList<TaskListReference> lists) =>
        "Task lists: " + string.Join(", ", lists.Select(list =>
            $"[{EscapeMarkdown(list.DisplayName)}](/tasks/{Uri.EscapeDataString(list.Id)})"));

    private static string EscapeMarkdown(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        lock (_reviewLock)
        {
            if (_disposed) return;
            _disposed = true;
            _pending.Clear();
            _reviewCancellation.Cancel();
            _reviewCancellation.Dispose();
        }
    }
}
