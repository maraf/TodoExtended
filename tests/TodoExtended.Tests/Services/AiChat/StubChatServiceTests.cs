using TodoExtended.Web.Services.AiChat;

namespace TodoExtended.Tests.Services.AiChat;

public class StubChatServiceTests
{
    [Fact]
    public async Task SendMessageAsync_ReturnsNotConfiguredMessage()
    {
        // Arrange
        var stubService = new StubChatService();
        var userMessage = "Create a task for tomorrow";
        var history = Array.Empty<ChatMessage>().ToList().AsReadOnly();

        // Act
        var response = await stubService.SendMessageAsync(userMessage, history);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("not configured", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ApiKey", response.Text);
        Assert.Empty(response.ProposedActions);
    }

    [Fact]
    public async Task SendMessageAsync_WithHistory_StillReturnsNotConfiguredMessage()
    {
        // Arrange
        var stubService = new StubChatService();
        var userMessage = "What about my tasks?";
        var history = new List<ChatMessage>
        {
            new("user", "Show me tasks", null, DateTimeOffset.UtcNow.AddMinutes(-5)),
            new("assistant", "AI chat is not configured.", null, DateTimeOffset.UtcNow.AddMinutes(-4))
        }.AsReadOnly();

        // Act
        var response = await stubService.SendMessageAsync(userMessage, history);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("not configured", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(response.ProposedActions);
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_CompletesImmediately()
    {
        // Arrange
        var stubService = new StubChatService();
        var userMessage = "Create a task";
        var history = Array.Empty<ChatMessage>().ToList().AsReadOnly();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var response = await stubService.SendMessageAsync(userMessage, history, cts.Token);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("not configured", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteActionsAsync_ReturnsEmptyResults()
    {
        // Arrange
        var stubService = new StubChatService();
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CreateTask, "Create task", new Dictionary<string, string>
            {
                ["taskListId"] = "list-123",
                ["title"] = "Test task"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation>
        {
            new(0, true)
        }.AsReadOnly();

        // Act
        var results = await stubService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task ExecuteActionsAsync_WithEmptyActions_ReturnsEmptyResults()
    {
        // Arrange
        var stubService = new StubChatService();
        var actions = Array.Empty<ProposedAction>().ToList().AsReadOnly();
        var confirmations = Array.Empty<ActionConfirmation>().ToList().AsReadOnly();

        // Act
        var results = await stubService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task ExecuteActionsAsync_WithCancellationToken_CompletesImmediately()
    {
        // Arrange
        var stubService = new StubChatService();
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CompleteTask, "Complete task", new Dictionary<string, string>
            {
                ["taskListId"] = "list-123",
                ["taskId"] = "task-456"
            })
        }.AsReadOnly();
        var confirmations = new List<ActionConfirmation>
        {
            new(0, true)
        }.AsReadOnly();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var results = await stubService.ExecuteActionsAsync(actions, confirmations, cts.Token);

        // Assert
        Assert.Empty(results);
    }
}
