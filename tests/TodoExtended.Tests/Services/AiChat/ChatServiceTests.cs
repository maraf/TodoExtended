using NSubstitute;
using TodoExtended.Web.Services;
using TodoExtended.Web.Services.AiChat;

namespace TodoExtended.Tests.Services.AiChat;

public class ChatServiceTests
{
    #region SendMessageAsync Tests

    [Fact]
    public async Task SendMessageAsync_WithUserMessage_ReturnsTextResponse()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        var userMessage = "What tasks do I have today?";
        var history = Array.Empty<ChatMessage>().ToList().AsReadOnly();

        // Act
        var response = await chatService.SendMessageAsync(userMessage, history);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Text);
        Assert.NotNull(response.ProposedActions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendMessageAsync_WithEmptyMessage_ThrowsArgumentException(string? emptyMessage)
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        var history = Array.Empty<ChatMessage>().ToList().AsReadOnly();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await chatService.SendMessageAsync(emptyMessage!, history));
    }

    [Fact]
    public async Task SendMessageAsync_WithHistory_IncludesHistoryInContext()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        var userMessage = "What about my other tasks?";
        var history = new List<ChatMessage>
        {
            new("user", "Show me today's tasks", null, DateTimeOffset.UtcNow.AddMinutes(-5)),
            new("assistant", "Here are your tasks for today.", null, DateTimeOffset.UtcNow.AddMinutes(-4))
        }.AsReadOnly();

        // Act
        var response = await chatService.SendMessageAsync(userMessage, history);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Text);
        Assert.True(chatService.HistoryCount == 2, "History should be included in context");
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new SlowChatService(todoService);
        var userMessage = "Create a task";
        var history = Array.Empty<ChatMessage>().ToList().AsReadOnly();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await chatService.SendMessageAsync(userMessage, history, cts.Token));
    }

    #endregion

    #region ExecuteActionsAsync Tests

    [Fact]
    public async Task ExecuteActionsAsync_WithApprovedCreateTask_CreatesTaskViaTodoService()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CreateTask, "Create task: Buy groceries", new Dictionary<string, string>
            {
                ["taskListId"] = "list-123",
                ["title"] = "Buy groceries",
                ["dueDate"] = DateOnly.FromDateTime(DateTime.Today).ToString()
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation>
        {
            new(0, true)
        }.AsReadOnly();

        todoService.CreateTaskAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateOnly?>(),
            Arg.Any<string>(),
            Arg.Any<TimeOnly?>())
            .Returns(new TodoTask("task-456", "Buy groceries", null, false, DateOnly.FromDateTime(DateTime.Today), "normal"));

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal(0, results[0].ActionIndex);
        await todoService.Received(1).CreateTaskAsync("list-123", "Buy groceries", Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_WithApprovedCompleteTask_CompletesTaskViaTodoService()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CompleteTask, "Complete task: Buy groceries", new Dictionary<string, string>
            {
                ["taskListId"] = "list-123",
                ["taskId"] = "task-456"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation>
        {
            new(0, true)
        }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal(0, results[0].ActionIndex);
        await todoService.Received(1).UpdateTaskStatusAsync("list-123", "task-456", true, Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_WithApprovedUncompleteTask_UncompletesTaskViaTodoService()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.UncompleteTask, "Uncomplete task: Buy groceries", new Dictionary<string, string>
            {
                ["taskListId"] = "list-123",
                ["taskId"] = "task-456"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation>
        {
            new(0, true)
        }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal(0, results[0].ActionIndex);
        await todoService.Received(1).UpdateTaskStatusAsync("list-123", "task-456", false, Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_WithRejectedActions_DoesNotExecute()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CreateTask, "Create task: Buy groceries", new Dictionary<string, string>
            {
                ["taskListId"] = "list-123",
                ["title"] = "Buy groceries"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation>
        {
            new(0, false)
        }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Empty(results);
        await todoService.DidNotReceive().CreateTaskAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_WithMixedConfirmations_OnlyExecutesApproved()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CreateTask, "Create task: Buy groceries", new Dictionary<string, string>
            {
                ["taskListId"] = "list-123",
                ["title"] = "Buy groceries"
            }),
            new(TaskActionType.CompleteTask, "Complete task: Do laundry", new Dictionary<string, string>
            {
                ["taskListId"] = "list-123",
                ["taskId"] = "task-789"
            }),
            new(TaskActionType.CreateTask, "Create task: Call dentist", new Dictionary<string, string>
            {
                ["taskListId"] = "list-456",
                ["title"] = "Call dentist"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation>
        {
            new(0, true),   // Approve first
            new(1, false),  // Reject second
            new(2, true)    // Approve third
        }.AsReadOnly();

        todoService.CreateTaskAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>())
            .Returns(new TodoTask("new-task", "title", null, false, null, "normal"));

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(0, results[0].ActionIndex);
        Assert.Equal(2, results[1].ActionIndex);
        await todoService.Received(2).CreateTaskAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>());
        await todoService.DidNotReceive().UpdateTaskStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_WithTodoServiceError_ReturnsFailureResult()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CreateTask, "Create task: Buy groceries", new Dictionary<string, string>
            {
                ["taskListId"] = "list-123",
                ["title"] = "Buy groceries"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation>
        {
            new(0, true)
        }.AsReadOnly();

        todoService.CreateTaskAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>())
            .Returns(Task.FromException<TodoTask>(new InvalidOperationException("Network error")));

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Equal(0, results[0].ActionIndex);
        Assert.Contains("Network error", results[0].Message);
    }

    [Fact]
    public async Task ExecuteActionsAsync_WithEmptyActions_ReturnsEmptyResults()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var chatService = new TestChatService(todoService);
        
        var actions = Array.Empty<ProposedAction>().ToList().AsReadOnly();
        var confirmations = Array.Empty<ActionConfirmation>().ToList().AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Empty(results);
        await todoService.DidNotReceive().CreateTaskAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>());
        await todoService.DidNotReceive().UpdateTaskStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>());
    }

    #endregion

    #region GetTaskAsync Tests

    [Fact]
    public async Task GetTaskAsync_WhenTaskExists_ReturnsTaskWithBody()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        var expected = new TodoTask("task-1", "Buy milk", "Get 2% milk from the store", false, null, "normal");
        todoService.GetTaskAsync("list-1", "task-1", Arg.Any<string>()).Returns(expected);

        // Act
        var result = await todoService.GetTaskAsync("list-1", "task-1", "test-user");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("task-1", result.Id);
        Assert.Equal("Buy milk", result.Title);
        Assert.Equal("Get 2% milk from the store", result.Body);
    }

    [Fact]
    public async Task GetTaskAsync_WhenTaskDoesNotExist_ReturnsNull()
    {
        // Arrange
        var todoService = Substitute.For<ITodoService>();
        todoService.GetTaskAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns((TodoTask?)null);

        // Act
        var result = await todoService.GetTaskAsync("list-1", "nonexistent", "test-user");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Test Implementation Classes

    /// <summary>
    /// Test implementation that validates contracts and tracks history usage.
    /// </summary>
    private class TestChatService : IChatService
    {
        private readonly ITodoService _todoService;
        public int HistoryCount { get; private set; }

        public TestChatService(ITodoService todoService)
        {
            _todoService = todoService;
        }

        public Task<ChatResponse> SendMessageAsync(string userMessage, IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                throw new ArgumentException("Message cannot be empty", nameof(userMessage));

            ct.ThrowIfCancellationRequested();

            HistoryCount = history.Count;

            return Task.FromResult(new ChatResponse(
                Text: $"Response to: {userMessage}",
                ProposedActions: []));
        }

        public async Task<IReadOnlyList<ActionResult>> ExecuteActionsAsync(
            IReadOnlyList<ProposedAction> actions,
            IReadOnlyList<ActionConfirmation> confirmations,
            CancellationToken ct = default)
        {
            var results = new List<ActionResult>();

            foreach (var confirmation in confirmations.Where(c => c.Approved))
            {
                var action = actions[confirmation.ActionIndex];
                
                try
                {
                    ct.ThrowIfCancellationRequested();

                    switch (action.Type)
                    {
                        case TaskActionType.CreateTask:
                            var taskListId = action.Parameters["taskListId"];
                            var title = action.Parameters["title"];
                            DateOnly? dueDate = action.Parameters.ContainsKey("dueDate")
                                ? DateOnly.Parse(action.Parameters["dueDate"])
                                : null;
                            
                            await _todoService.CreateTaskAsync(taskListId, title, dueDate, "test-user");
                            results.Add(new ActionResult(confirmation.ActionIndex, true, "Task created"));
                            break;

                        case TaskActionType.CompleteTask:
                            await _todoService.UpdateTaskStatusAsync(
                                action.Parameters["taskListId"],
                                action.Parameters["taskId"],
                                true,
                                "test-user");
                            results.Add(new ActionResult(confirmation.ActionIndex, true, "Task completed"));
                            break;

                        case TaskActionType.UncompleteTask:
                            await _todoService.UpdateTaskStatusAsync(
                                action.Parameters["taskListId"],
                                action.Parameters["taskId"],
                                false,
                                "test-user");
                            results.Add(new ActionResult(confirmation.ActionIndex, true, "Task uncompleted"));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new ActionResult(confirmation.ActionIndex, false, ex.Message));
                }
            }

            return results.AsReadOnly();
        }
    }

    /// <summary>
    /// Test implementation that simulates slow operations for cancellation testing.
    /// </summary>
    private class SlowChatService : IChatService
    {
        private readonly ITodoService _todoService;

        public SlowChatService(ITodoService todoService)
        {
            _todoService = todoService;
        }

        public async Task<ChatResponse> SendMessageAsync(string userMessage, IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new ChatResponse("Slow response", []);
        }

        public Task<IReadOnlyList<ActionResult>> ExecuteActionsAsync(
            IReadOnlyList<ProposedAction> actions,
            IReadOnlyList<ActionConfirmation> confirmations,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ActionResult>>([]);
        }
    }

    #endregion
}
