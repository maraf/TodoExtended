using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TodoExtended.Web.Data;
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

    #region get_current_datetime Tool Tests

    [Fact]
    public async Task SendMessageAsync_WhenAiCallsGetCurrentDatetime_ReturnsValidJsonWithOffset()
    {
        // Arrange — use a fixed timezone with a known non-UTC offset to verify DST-aware output
        var easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")
                        ?? TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var (chatService, _, _, chatClient) = CreateRealChatServiceWithClient(easternTz);

        const string callId = "call-dt-1";

        // First AI response: the model calls get_current_datetime
        var toolCallResponse = new Microsoft.Extensions.AI.ChatResponse(
        [
            new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.Assistant,
                [new Microsoft.Extensions.AI.FunctionCallContent(callId, "get_current_datetime")])
        ]);

        // Capture the tool-result message fed back to the model
        List<Microsoft.Extensions.AI.ChatMessage>? capturedMessages = null;
        var textResponse = new Microsoft.Extensions.AI.ChatResponse(
        [
            new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.Assistant,
                [new Microsoft.Extensions.AI.TextContent("The current time has been obtained.")])
        ]);

        chatClient
            .GetResponseAsync(
                Arg.Do<IList<Microsoft.Extensions.AI.ChatMessage>>(msgs => capturedMessages = [.. msgs]),
                Arg.Any<Microsoft.Extensions.AI.ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(toolCallResponse, textResponse);

        // Act
        var response = await chatService.SendMessageAsync("What time is it?", []);

        // Assert: the service should have made a second call with the tool result included
        Assert.NotNull(capturedMessages);

        // Find the tool-result message for get_current_datetime
        var toolResultMessage = capturedMessages
            .Where(m => m.Role == Microsoft.Extensions.AI.ChatRole.Tool)
            .SelectMany(m => m.Contents.OfType<Microsoft.Extensions.AI.FunctionResultContent>())
            .FirstOrDefault(r => r.CallId == callId);

        Assert.NotNull(toolResultMessage);
        Assert.False(string.IsNullOrEmpty(toolResultMessage.Result?.ToString()), "Tool result should not be empty");

        // Verify JSON shape: must have DateTimeOffset (ISO-8601 with offset), TimeZoneId, UtcOffsetMinutes
        var json = toolResultMessage.Result!.ToString()!;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("DateTimeOffset", out var dtoProp), "Missing DateTimeOffset property");
        Assert.True(root.TryGetProperty("TimeZoneId", out var tzIdProp), "Missing TimeZoneId property");
        Assert.True(root.TryGetProperty("UtcOffsetMinutes", out var offsetProp), "Missing UtcOffsetMinutes property");

        // ISO-8601 round-trip check
        Assert.True(
            DateTimeOffset.TryParseExact(dtoProp.GetString(), "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out _),
            $"DateTimeOffset '{dtoProp.GetString()}' is not a valid ISO-8601 round-trip string");

        Assert.Equal(easternTz.Id, tzIdProp.GetString());

        // Offset should be the actual current Eastern offset (-5h = -300 or -4h = -240 during DST)
        var expectedOffset = (int)easternTz.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
        Assert.Equal(expectedOffset, offsetProp.GetInt32());
    }

    [Fact]
    public async Task SendMessageAsync_GetCurrentDatetimeNotInWriteTools_IsAutoInvoked()
    {
        // Arrange — verify the tool is treated as a read tool (auto-invoked, not a proposed action)
        var (chatService, _, _, chatClient) = CreateRealChatServiceWithClient();

        const string callId = "call-dt-2";
        var toolCallResponse = new Microsoft.Extensions.AI.ChatResponse(
        [
            new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.Assistant,
                [new Microsoft.Extensions.AI.FunctionCallContent(callId, "get_current_datetime")])
        ]);

        var textResponse = new Microsoft.Extensions.AI.ChatResponse(
        [
            new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.Assistant,
                [new Microsoft.Extensions.AI.TextContent("Here is the time.")])
        ]);

        chatClient
            .GetResponseAsync(Arg.Any<IList<Microsoft.Extensions.AI.ChatMessage>>(), Arg.Any<Microsoft.Extensions.AI.ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(toolCallResponse, textResponse);

        // Act
        var response = await chatService.SendMessageAsync("What time is it?", []);

        // Assert: no proposed actions — the tool should have been auto-invoked as a read tool
        Assert.Empty(response.ProposedActions);
        Assert.Equal("Here is the time.", response.Text);
    }

    #endregion

    #region CreateTask with Reminder Tests

    [Fact]
    public async Task ExecuteActionsAsync_CreateTask_WithValidReminderTime_PassesReminderToService()
    {
        // Arrange
        var (chatService, todoService, _) = CreateRealChatService();

        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CreateTask, "Create task \"Buy milk\" with reminder at 09:00", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["title"] = "Buy milk",
                ["reminderTime"] = "09:00"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, true) }.AsReadOnly();

        todoService.CreateTaskAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>())
            .Returns(new TodoTask("task-456", "Buy milk", null, false, null, "normal", true));

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        await todoService.Received(1).CreateTaskAsync(
            "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
            "Buy milk",
            Arg.Any<DateOnly?>(),
            Arg.Any<string>(),
            new TimeOnly(9, 0));
    }

    [Fact]
    public async Task ExecuteActionsAsync_CreateTask_WithInvalidReminderTime_ReturnsFailureResult()
    {
        // Arrange
        var (chatService, todoService, _) = CreateRealChatService();

        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CreateTask, "Create task \"Buy milk\" with reminder at not-a-time", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["title"] = "Buy milk",
                ["reminderTime"] = "not-a-time"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, true) }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Contains("reminderTime", results[0].Message);
        await todoService.DidNotReceive().CreateTaskAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_CreateTask_WithSingleDigitHourReminderTime_PassesReminderToService()
    {
        // Arrange
        var (chatService, todoService, _) = CreateRealChatService();

        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CreateTask, "Create task \"Buy milk\" with reminder at 9:00", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["title"] = "Buy milk",
                ["reminderTime"] = "9:00"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, true) }.AsReadOnly();

        todoService.CreateTaskAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>())
            .Returns(new TodoTask("task-456", "Buy milk", null, false, null, "normal", true));

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        await todoService.Received(1).CreateTaskAsync(
            "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
            "Buy milk",
            Arg.Any<DateOnly?>(),
            Arg.Any<string>(),
            new TimeOnly(9, 0));
    }

    [Fact]
    public async Task ExecuteActionsAsync_CreateTask_WithoutReminderTime_PassesNullReminderToService()
    {
        // Arrange
        var (chatService, todoService, _) = CreateRealChatService();

        var actions = new List<ProposedAction>
        {
            new(TaskActionType.CreateTask, "Create task \"Buy milk\"", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["title"] = "Buy milk"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, true) }.AsReadOnly();

        todoService.CreateTaskAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly?>(), Arg.Any<string>(), Arg.Any<TimeOnly?>())
            .Returns(new TodoTask("task-456", "Buy milk", null, false, null, "normal"));

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        await todoService.Received(1).CreateTaskAsync(
            "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
            "Buy milk",
            Arg.Any<DateOnly?>(),
            Arg.Any<string>(),
            null);
    }

    #endregion

    #region SetReminder Tests

    [Fact]
    public async Task ExecuteActionsAsync_SetReminder_WithValidTimeAndDate_CallsSetTaskReminderAsync()
    {
        // Arrange
        var (chatService, todoService, _) = CreateRealChatService();

        var reminderDate = new DateOnly(2026, 4, 1);
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.SetReminder, "Set reminder on task \"Buy milk\" at 09:00", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["taskId"] = "AQMkADAAATM0MDAAMS10YXNrLTQ1NgAAAA==",
                ["reminderTime"] = "09:00",
                ["reminderDate"] = reminderDate.ToString("yyyy-MM-dd")
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, true) }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        await todoService.Received(1).SetTaskReminderAsync(
            "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
            "AQMkADAAATM0MDAAMS10YXNrLTQ1NgAAAA==",
            reminderDate,
            new TimeOnly(9, 0),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_SetReminder_WithInvalidReminderTime_ReturnsFailureResult()
    {
        // Arrange
        var (chatService, todoService, _) = CreateRealChatService();

        var actions = new List<ProposedAction>
        {
            new(TaskActionType.SetReminder, "Set reminder", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["taskId"] = "AQMkADAAATM0MDAAMS10YXNrLTQ1NgAAAA==",
                ["reminderTime"] = "not-a-time"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, true) }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Contains("reminderTime", results[0].Message);
        await todoService.DidNotReceive().SetTaskReminderAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_SetReminder_WithInvalidReminderDate_ReturnsFailureResult()
    {
        // Arrange
        var (chatService, todoService, _) = CreateRealChatService();

        var actions = new List<ProposedAction>
        {
            new(TaskActionType.SetReminder, "Set reminder", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["taskId"] = "AQMkADAAATM0MDAAMS10YXNrLTQ1NgAAAA==",
                ["reminderTime"] = "09:00",
                ["reminderDate"] = "not-a-date"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, true) }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Contains("reminderDate", results[0].Message);
        await todoService.DidNotReceive().SetTaskReminderAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_SetReminder_WithNoReminderDate_DefaultsToUserTimezoneToday()
    {
        // Arrange — pin "today" to a fixed date to avoid flakiness around midnight
        var pinnedToday = new DateOnly(2026, 6, 15);
        var (chatService, todoService, _) = CreateRealChatService(TimeZoneInfo.Utc, pinnedToday);

        var actions = new List<ProposedAction>
        {
            new(TaskActionType.SetReminder, "Set reminder at 09:00", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["taskId"] = "AQMkADAAATM0MDAAMS10YXNrLTQ1NgAAAA==",
                ["reminderTime"] = "09:00"
                // no reminderDate — should default to user-timezone today
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, true) }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        await todoService.Received(1).SetTaskReminderAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            pinnedToday,
            new TimeOnly(9, 0),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_SetReminder_WithSingleDigitHour_CallsSetTaskReminderAsync()
    {
        // Arrange — "9:00" (no leading zero) should be accepted and parsed as 09:00
        var (chatService, todoService, _) = CreateRealChatService();

        var reminderDate = new DateOnly(2026, 4, 1);
        var actions = new List<ProposedAction>
        {
            new(TaskActionType.SetReminder, "Set reminder on task \"Buy milk\" at 9:00", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["taskId"] = "AQMkADAAATM0MDAAMS10YXNrLTQ1NgAAAA==",
                ["reminderTime"] = "9:00",
                ["reminderDate"] = reminderDate.ToString("yyyy-MM-dd")
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, true) }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Single(results);
        await todoService.Received(1).SetTaskReminderAsync(
            "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
            "AQMkADAAATM0MDAAMS10YXNrLTQ1NgAAAA==",
            reminderDate,
            new TimeOnly(9, 0),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteActionsAsync_SetReminder_WithRejectedConfirmation_DoesNotCallService()
    {
        // Arrange
        var (chatService, todoService, _) = CreateRealChatService();

        var actions = new List<ProposedAction>
        {
            new(TaskActionType.SetReminder, "Set reminder at 09:00", new Dictionary<string, string>
            {
                ["listId"] = "AQMkADAAATM0MDAAMS1saXN0LTEyMwAAAA==",
                ["taskId"] = "AQMkADAAATM0MDAAMS10YXNrLTQ1NgAAAA==",
                ["reminderTime"] = "09:00"
            })
        }.AsReadOnly();

        var confirmations = new List<ActionConfirmation> { new(0, false) }.AsReadOnly();

        // Act
        var results = await chatService.ExecuteActionsAsync(actions, confirmations);

        // Assert
        Assert.Empty(results);
        await todoService.DidNotReceive().SetTaskReminderAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<string>());
    }

    /// <summary>
    /// Creates a real <see cref="ChatService"/> with mocked dependencies for testing.
    /// </summary>
    private static (ChatService chatService, ITodoService todoService, IUserTimeZoneService userTimeZoneService)
        CreateRealChatService(TimeZoneInfo? userTimeZone = null, DateOnly? pinnedToday = null)
    {
        var (chatService, todoService, userTimeZoneService, _) = CreateRealChatServiceWithClient(userTimeZone, pinnedToday);
        return (chatService, todoService, userTimeZoneService);
    }

    private static (ChatService chatService, ITodoService todoService, IUserTimeZoneService userTimeZoneService, Microsoft.Extensions.AI.IChatClient chatClient)
        CreateRealChatServiceWithClient(TimeZoneInfo? userTimeZone = null, DateOnly? pinnedToday = null)
    {
        var todoService = Substitute.For<ITodoService>();
        var templateService = Substitute.For<ITemplateService>();
        var chatClient = Substitute.For<Microsoft.Extensions.AI.IChatClient>();

        userTimeZone ??= TimeZoneInfo.Utc;
        // Use a concrete implementation so the GetTodayAsync() default interface method works correctly.
        var userTimeZoneService = new FixedTimeZoneService(userTimeZone, pinnedToday);

        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var options = Options.Create(new AiChatOptions { MaxHistoryMessages = 20 });

        var dbContextFactory = Substitute.For<IDbContextFactory<AppDbContext>>();

        var chatService = new ChatService(
            chatClient,
            todoService,
            templateService,
            httpContextAccessor,
            userTimeZoneService,
            dbContextFactory,
            options,
            NullLogger<ChatService>.Instance);

        return (chatService, todoService, userTimeZoneService, chatClient);
    }

    /// <summary>
    /// Minimal <see cref="IUserTimeZoneService"/> implementation that returns a fixed timezone and fixed "today" date.
    /// </summary>
    private class FixedTimeZoneService(TimeZoneInfo timeZone, DateOnly? pinnedToday = null) : IUserTimeZoneService
    {
        public Task<TimeZoneInfo> GetCurrentUserTimeZoneAsync() => Task.FromResult(timeZone);

        public Task<DateOnly> GetTodayAsync() =>
            Task.FromResult(pinnedToday ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone)));
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
