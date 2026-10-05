using Microsoft.AspNetCore.Components.AI;
using Microsoft.Extensions.AI;
using TodoExtended.Web.Services.AiChat;
using AiMessage = Microsoft.Extensions.AI.ChatMessage;
using AppMessage = TodoExtended.Web.Services.AiChat.ChatMessage;
using AppResponse = TodoExtended.Web.Services.AiChat.ChatResponse;

namespace TodoExtended.Tests.Services.AiChat;

public class TaskAgentChatClientTests
{
    [Fact]
    public async Task StreamsTextBeforeTheServiceCompletes()
    {
        var service = new TestService { PauseAfterFirstChunk = true };
        using var client = new TaskAgentChatClient(service);
        await using var stream = client.GetStreamingResponseAsync(
            [new AiMessage(ChatRole.User, "hello")]).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal("First", stream.Current.Text);
        Assert.False(service.Release.Task.IsCompleted);
        service.Release.SetResult();
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal(" second", stream.Current.Text);
    }

    [Fact]
    public async Task DoesNotDuplicateTheLatestUserMessage()
    {
        var service = new TestService();
        using var client = new TaskAgentChatClient(service);
        await client.GetResponseAsync([
            new AiMessage(ChatRole.User, "previous"),
            new AiMessage(ChatRole.Assistant, "answer"),
            new AiMessage(ChatRole.User, "latest"),
        ]);
        Assert.Equal("latest", service.LastMessage);
        Assert.Equal(["previous", "answer"], service.History.Select(message => message.Text));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyExplicitApprovalExecutesTheServerHeldProposalOnce(bool approved)
    {
        var service = new TestService { Propose = true };
        using var client = new TaskAgentChatClient(service);
        var response = await client.GetResponseAsync([new AiMessage(ChatRole.User, "create")]);
        var call = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        Assert.Equal(0, service.ExecutionCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReviewActionAsync(call.CallId));
        var result = await client.ReviewActionAsync(call.CallId, approved);
        Assert.Equal(approved ? 1 : 0, service.ExecutionCount);
        Assert.Equal(approved, result.Success);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReviewActionAsync(call.CallId, true));
    }

    [Fact]
    public async Task CancelingInvalidatesPendingProposals()
    {
        var service = new TestService { Propose = true };
        using var client = new TaskAgentChatClient(service);
        var response = await client.GetResponseAsync([new AiMessage(ChatRole.User, "create")]);
        var call = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        client.ClearPending();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReviewActionAsync(call.CallId, true));
        Assert.Equal(0, service.ExecutionCount);
    }

    [Fact]
    public async Task UIAgentPausesForReviewAndContinuesWithTheResult()
    {
        var service = new TestService { Propose = true };
        using var client = new TaskAgentChatClient(service);
        using var agent = new UIAgent(client, options => options.RegisterUIAction(
            AIFunctionFactory.Create(client.ReviewActionAsync, TaskAgentChatClient.ReviewToolName)));
        using var context = new AgentContext(agent);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = context.RegisterOnStatusChanged(status =>
        {
            if (status == ConversationStatus.AwaitingInput)
                waiting.TrySetResult();
        });
        var run = context.SendMessageAsync("create");
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, service.ExecutionCount);
        var action = Assert.Single(context.Turns.Single().ResponseBlocks.OfType<UIActionBlock>());
        action.Call.Arguments!["approved"] = true;
        await action.InvokeAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ConversationStatus.Idle, context.Status);
        Assert.Equal(1, service.ExecutionCount);
        Assert.Contains(context.Turns.Single().ResponseBlocks.OfType<RichContentBlock>(),
            block => block.RawText.Contains("Created"));
    }

    [Fact]
    public async Task MultipleProposalsRequireSeparateDecisions()
    {
        var service = new TestService { Propose = true, ProposalCount = 2 };
        using var client = new TaskAgentChatClient(service);
        using var agent = new UIAgent(client, options => options.RegisterUIAction(
            AIFunctionFactory.Create(client.ReviewActionAsync, TaskAgentChatClient.ReviewToolName)));
        using var context = new AgentContext(agent);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = context.RegisterOnStatusChanged(status =>
        {
            if (status == ConversationStatus.AwaitingInput) waiting.TrySetResult();
        });
        var run = context.SendMessageAsync("create two");
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var actions = context.Turns.Single().ResponseBlocks.OfType<UIActionBlock>().ToList();
        Assert.Equal(2, actions.Count);
        actions[0].Call.Arguments!["approved"] = true;
        await actions[0].InvokeAsync();
        Assert.False(run.IsCompleted);
        actions[1].Call.Arguments!["approved"] = false;
        await actions[1].InvokeAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, service.ExecutionCount);
    }

    [Fact]
    public async Task HistoryRoundTripsAsDisplayOnlyProposalsWithResults()
    {
        var service = new TestService { Propose = true };
        using var client = new TaskAgentChatClient(service);
        var thread = new BrowserConversationThread();
        using var agent = new UIAgent(client, options =>
        {
            options.Thread = thread;
            options.RegisterUIAction(AIFunctionFactory.Create(client.ReviewActionAsync, TaskAgentChatClient.ReviewToolName));
        });
        using var context = new AgentContext(agent);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = context.RegisterOnStatusChanged(status =>
        {
            if (status == ConversationStatus.AwaitingInput) waiting.TrySetResult();
        });
        var run = context.SendMessageAsync("create");
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var action = Assert.Single(context.Turns.Single().ResponseBlocks.OfType<UIActionBlock>());
        action.Call.Arguments!["approved"] = true;
        await action.InvokeAsync();
        await run;

        var restored = new BrowserConversationThread();
        restored.Load(thread.Serialize());
        Assert.All(restored.GetUpdates().SelectMany(update => update.Contents).OfType<FunctionCallContent>(),
            call => Assert.True(call.InformationalOnly));
        Assert.Contains(restored.GetUpdates().SelectMany(update => update.Contents).OfType<FunctionResultContent>(),
            result => TaskAgentChatClient.ReadResult(result.Result).Success);
        using var restoredAgent = new UIAgent(client, options => options.Thread = restored);
        var blocks = await restoredAgent.RestoreAsync();
        Assert.DoesNotContain(blocks, block => block is UIActionBlock);
    }

    [Fact]
    public async Task FailuresReachTheAgentErrorState()
    {
        using var client = new TaskAgentChatClient(new TestService { Fail = true });
        using var agent = new UIAgent(client);
        using var context = new AgentContext(agent);
        await context.SendMessageAsync("hello");
        Assert.Equal(ConversationStatus.Error, context.Status);
        Assert.IsType<InvalidOperationException>(context.Error);
    }

    [Fact]
    public void MigratesCamelCaseLegacyHistoryWithResultsAndTaskListLinks()
    {
        const string json = """
            [{"message":{"role":"assistant","text":"Shopping","proposedActions":[{"type":0,
            "description":"Create milk","parameters":{"title":"Milk"}}],
            "timestamp":"2026-01-10T09:00:00Z","taskListReferences":[{"id":"shopping","displayName":"Shopping"}]},
            "results":[{"actionIndex":0,"success":true,"message":"Created"}]}]
            """;
        var thread = new BrowserConversationThread();
        thread.LoadLegacy(json);
        Assert.Contains("[Shopping](/tasks/shopping)", thread.GetUpdates()[0].Text);
        var call = Assert.Single(thread.GetUpdates().SelectMany(update => update.Contents).OfType<FunctionCallContent>());
        Assert.True(call.InformationalOnly);
        Assert.Equal("Milk", TaskAgentChatClient.ReadAction(call.Arguments).Parameters["title"]);
        Assert.True(TaskAgentChatClient.ReadResult(Assert.Single(thread.GetUpdates()
            .SelectMany(update => update.Contents).OfType<FunctionResultContent>()).Result).Success);
    }

    [Fact]
    public void CoalescesStreamedTextAndCapsHistoryAtCompleteUserTurns()
    {
        var thread = new BrowserConversationThread();
        for (var i = 0; i < 205; i++)
        {
            thread.AppendUserMessage(new AiMessage(ChatRole.User, $"Message {i}"));
            thread.AppendUpdate(new ChatResponseUpdate(ChatRole.Assistant, "One") { MessageId = $"reply{i}" });
            thread.AppendUpdate(new ChatResponseUpdate(ChatRole.Assistant, " two") { MessageId = $"reply{i}" });
            thread.CompleteTurn();
        }
        Assert.Equal(400, thread.GetUpdates().Count);
        Assert.Equal("Message 5", thread.GetUpdates()[0].Text);
        Assert.Equal("One two", thread.GetUpdates()[1].Text);
    }

    private sealed class TestService : IChatService
    {
        public bool PauseAfterFirstChunk { get; init; }
        public bool Propose { get; init; }
        public int ProposalCount { get; init; } = 1;
        public bool Fail { get; init; }
        public int ExecutionCount { get; private set; }
        public string? LastMessage { get; private set; }
        public IReadOnlyList<AppMessage> History { get; private set; } = [];
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AppResponse> SendMessageAsync(string userMessage, IReadOnlyList<AppMessage> history, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async Task<AppResponse> SendStreamingMessageAsync(string userMessage,
            IReadOnlyList<AppMessage> history, Func<string, Task> onText, CancellationToken ct = default)
        {
            LastMessage = userMessage;
            History = history;
            if (Fail) throw new InvalidOperationException("Provider failed");
            await onText("First");
            if (PauseAfterFirstChunk) await Release.Task.WaitAsync(ct);
            await onText(" second");
            return new AppResponse("First second", Propose
                ? Enumerable.Range(0, ProposalCount).Select(i =>
                    new ProposedAction(TaskActionType.CreateTask, "Create test task", new() { ["title"] = $"test{i}" })).ToList()
                : []);
        }

        public Task<IReadOnlyList<ActionResult>> ExecuteActionsAsync(IReadOnlyList<ProposedAction> actions,
            IReadOnlyList<ActionConfirmation> confirmations, CancellationToken ct = default)
        {
            ExecutionCount++;
            return Task.FromResult<IReadOnlyList<ActionResult>>([new ActionResult(0, true, "Created")]);
        }
    }
}
