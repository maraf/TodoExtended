using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Components.AI;
using Microsoft.Extensions.AI;
using TodoExtended.Web.Services.AiChat;
using AiMessage = Microsoft.Extensions.AI.ChatMessage;

namespace TodoExtended.Tests.Services.AiChat;

public class ChatTimestampTests
{
    [Fact]
    public async Task ReusingUserContentDoesNotReuseItsPreviousTurnTimestamp()
    {
        var clock = new TestClock();
        var thread = new BrowserConversationThread(clock);
        using var client = new StreamingClient(clock);
        using var agent = CreateAgent(client, thread);
        var message = new AiMessage(ChatRole.User, "hello");
        var expected = new List<DateTimeOffset>();
        for (var turn = 0; turn < 2; turn++)
        {
            expected.Add(clock.GetUtcNow());
            await foreach (var _ in agent.SendMessageAsync(message)) { }
            clock.Advance();
        }
        Assert.Equal(expected, thread.GetUpdates().Where(update => update.Role == ChatRole.User)
            .Select(update => update.CreatedAt!.Value));
        Assert.Equal(2, thread.GetUpdates().Where(update => update.Role == ChatRole.User)
            .Select(update => update.MessageId).Distinct().Count());
    }

    [Fact]
    public async Task CappingPersistedHistoryDoesNotLoseTimestampsOfStillDisplayedBlocks()
    {
        var clock = new TestClock();
        var thread = new BrowserConversationThread(clock);
        using var client = new StreamingClient(clock);
        using var agent = CreateAgent(client, thread);
        RichContentBlock? firstUserBlock = null;
        var firstTimestamp = clock.GetUtcNow();
        for (var turn = 0; turn < 205; turn++)
        {
            await foreach (var block in agent.SendMessageAsync(new AiMessage(ChatRole.User, $"Turn {turn}")))
            {
                if (block is RichContentBlock rich && rich.Role == ChatRole.User)
                    firstUserBlock ??= rich;
            }
        }
        Assert.Equal(400, thread.GetUpdates().Count);
        Assert.Equal("Turn 5", thread.GetUpdates()[0].Text);
        Assert.NotNull(firstUserBlock);
        Assert.Equal(firstTimestamp, thread.GetTimestamp(firstUserBlock));
    }

    [Fact]
    public async Task NativeStreamingKeepsDistinctUserAndAssistantCreationTimesAcrossTurnsAndRestore()
    {
        var clock = new TestClock();
        var thread = new BrowserConversationThread(clock);
        using var client = new StreamingClient(clock);
        using var agent = CreateAgent(client, thread);
        var blocks = new List<RichContentBlock>();
        var expected = new List<DateTimeOffset>();
        for (var turn = 0; turn < 2; turn++)
        {
            var userTime = clock.GetUtcNow();
            expected.Add(userTime);
            expected.Add(userTime.AddMinutes(1));
            await foreach (var block in agent.SendMessageAsync(new AiMessage(ChatRole.User, "same text")))
            {
                if (block is not RichContentBlock rich)
                    continue;
                if (!blocks.Contains(rich))
                    blocks.Add(rich);
                Assert.Equal(rich.Role == ChatRole.User ? userTime : userTime.AddMinutes(1),
                    thread.GetTimestamp(rich));
            }
            clock.Advance();
        }
        Assert.Equal(4, blocks.Count);
        Assert.Equal(["same text", "One two", "same text", "One two"], blocks.Select(block => block.RawText));
        Assert.Equal(expected, blocks.Select(thread.GetTimestamp));
        Assert.Equal(expected, thread.GetUpdates().Select(update => update.CreatedAt!.Value));
        Assert.Equal(4, thread.GetUpdates().Count);

        var restored = new BrowserConversationThread(clock);
        restored.Load(thread.Serialize());
        using var restoredAgent = CreateAgent(client, restored);
        var restoredBlocks = (await restoredAgent.RestoreAsync()).OfType<RichContentBlock>().ToList();
        Assert.Equal(blocks.Select(block => block.Id), restoredBlocks.Select(block => block.Id));
        Assert.Equal(blocks.Select(block => block.RawText), restoredBlocks.Select(block => block.RawText));
        Assert.Equal(expected, restoredBlocks.Select(restored.GetTimestamp));
        clock.Advance();
        Assert.Equal(expected, restoredBlocks.Select(restored.GetTimestamp));
    }

    [Fact]
    public async Task LegacyMigrationPreservesOriginalTimestampsIncludingTheirOffsets()
    {
        const string json = """
            [{"message":{"role":"user","text":"same text","timestamp":"2026-01-10T09:00:00+02:00"}},
             {"message":{"role":"assistant","text":"same text","timestamp":"2026-01-10T09:01:00+02:00",
             "proposedActions":[{"type":0,"description":"Create milk","parameters":{"title":"Milk"}}]},
             "results":[{"actionIndex":0,"success":true,"message":"Created"}]}]
            """;
        var clock = new TestClock();
        var thread = new BrowserConversationThread(clock);
        thread.LoadLegacy(json);
        var userTime = DateTimeOffset.Parse("2026-01-10T09:00:00+02:00");
        var assistantTime = userTime.AddMinutes(1);
        Assert.Equal(userTime, thread.GetUpdates()[0].CreatedAt);
        Assert.All(thread.GetUpdates().Skip(1), update =>
        {
            Assert.Equal(assistantTime, update.CreatedAt);
            Assert.Equal(assistantTime.Offset, update.CreatedAt!.Value.Offset);
        });
        var restored = new BrowserConversationThread(clock);
        restored.Load(thread.Serialize());
        using var client = new StreamingClient(clock);
        using var agent = CreateAgent(client, restored);
        var blocks = (await agent.RestoreAsync()).OfType<RichContentBlock>().ToList();
        Assert.Equal([userTime, assistantTime], blocks.Select(restored.GetTimestamp));
        Assert.All(blocks, block => Assert.Equal(userTime.Offset, restored.GetTimestamp(block).Offset));
    }

    [Fact]
    public void CoalescingKeepsTheFirstFragmentTimestampRatherThanTheLatestProviderTime()
    {
        var thread = new BrowserConversationThread();
        var first = DateTimeOffset.Parse("2026-01-10T09:00:00Z");
        thread.AppendUpdate(new ChatResponseUpdate(ChatRole.Assistant, "One")
        {
            MessageId = "reply", CreatedAt = first,
        });
        thread.AppendUpdate(new ChatResponseUpdate(ChatRole.Assistant, " two")
        {
            MessageId = "reply", CreatedAt = first.AddMinutes(1),
        });
        var update = Assert.Single(thread.GetUpdates());
        Assert.Equal("One two", update.Text);
        Assert.Equal(first, update.CreatedAt);
        var restored = new BrowserConversationThread();
        restored.Load(thread.Serialize());
        Assert.Equal(first, Assert.Single(restored.GetUpdates()).CreatedAt);
    }

    [Fact]
    public async Task PreviouslyUntimestampedUpdatesGetOneStableIngestionTime()
    {
        var clock = new TestClock();
        var original = new[]
        {
            new ChatResponseUpdate(ChatRole.User, "hello"),
            new ChatResponseUpdate(ChatRole.Assistant, "One") { MessageId = "reply" },
            new ChatResponseUpdate(ChatRole.Assistant, " two") { MessageId = "reply" },
        };
        var thread = new BrowserConversationThread(clock);
        thread.Load(JsonSerializer.Serialize(original));
        var ingestionTime = clock.GetUtcNow();
        clock.Advance();
        using var client = new StreamingClient(clock);
        using var agent = CreateAgent(client, thread);
        Assert.All((await agent.RestoreAsync()).OfType<RichContentBlock>(),
            block => Assert.Equal(ingestionTime, thread.GetTimestamp(block)));
        var restored = new BrowserConversationThread(clock);
        restored.Load(thread.Serialize());
        Assert.All(restored.GetUpdates(), update => Assert.Equal(ingestionTime, update.CreatedAt));
    }

    private static UIAgent CreateAgent(IChatClient client, BrowserConversationThread thread) =>
        new(client, options =>
        {
            options.Thread = thread;
            options.AddBlockHandler(new ChatTimestampHandler(thread));
        });

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance() => _now = _now.AddMinutes(1);
    }

    private sealed class StreamingClient(TestClock clock) : IChatClient
    {
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<AiMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid().ToString("N");
            clock.Advance();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "One") { MessageId = id };
            await Task.Yield();
            clock.Advance();
            yield return new ChatResponseUpdate(ChatRole.Assistant, " two")
            {
                MessageId = id, CreatedAt = clock.GetUtcNow(),
            };
        }

        public Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(IEnumerable<AiMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
