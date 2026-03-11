using System.ClientModel;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using TodoExtended.Web.Services;

namespace TodoExtended.Web.Services.AiChat;

/// <summary>
/// Real AI chat implementation using Microsoft.Extensions.AI with tool-calling.
/// Read tools are auto-invoked; write tools become proposed actions for user confirmation.
/// </summary>
public class ChatService(
    IChatClient chatClient,
    ITodoService todoService,
    IOptions<AiChatOptions> options,
    ILogger<ChatService> logger) : IChatService
{
    private static readonly HashSet<string> WriteTools = ["create_task", "complete_task", "uncomplete_task"];

    private const string SystemPrompt = """
        You are a helpful task management assistant for the TodoExtended app.
        You help users manage their Microsoft To Do tasks using the available tools.

        Available capabilities:
        - View task lists and their tasks
        - View today's tasks across all lists
        - Get full details of a specific task (including description)
        - Create new tasks in specific lists
        - Mark tasks as complete or incomplete

        Guidelines:
        - Use the read tools to fetch current data before answering questions about tasks.
        - Use get_task_lists to discover lists, get_tasks or get_today_tasks to get task titles and IDs.
        - Use get_task to load full details (including description) for a specific task only when needed.
        - When creating tasks, always confirm which list to add them to.
        - Be concise and helpful.
        - Format task information clearly.
        - When listing tasks, include their completion status and due dates if available.

        CRITICAL: The listId and taskId parameters must be the exact "Id" field values
        returned by get_task_lists, get_tasks, get_today_tasks, or get_task.
        These are opaque API identifiers (e.g. "AQMkADAwATM0MDAAMS0..."), NOT display names.
        Never pass a task title or list name as an ID parameter.
        Always call a read tool first to obtain the correct Id values.
        """;

    public async Task<ChatResponse> SendMessageAsync(
        string userMessage,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        var tools = BuildTools();

        var messages = BuildConversation(userMessage, history, opts.MaxHistoryMessages);

        var chatOptions = new ChatOptions
        {
            Tools = tools,
            ToolMode = ChatToolMode.Auto,
        };

        var proposedActions = new List<ProposedAction>();
        var responseText = new StringBuilder();

        // Tool-calling loop: auto-invoke read tools, collect write tools as proposals
        const int maxIterations = 10;
        bool retried = false;

        for (int i = 0; i < maxIterations; i++)
        {
            logger.LogDebug("AI request iteration {Iteration} with {MessageCount} messages", i + 1, messages.Count);

            Microsoft.Extensions.AI.ChatResponse response;
            try
            {
                response = await chatClient.GetResponseAsync(messages, chatOptions, ct);
            }
            catch (ClientResultException ex) when (IsTokenLimitError(ex))
            {
                if (retried)
                {
                    logger.LogError(ex, "AI request still exceeds token limit after trimming");
                    return new ChatResponse("Sorry, the conversation is too long. Please start a new chat.", []);
                }

                logger.LogWarning("AI request hit token limit (HTTP {Status}), trimming conversation and retrying", ex.Status);
                retried = true;
                messages = TrimConversationForRetry(messages);
                i--; // Retry this iteration
                continue;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI chat request failed");
                return new ChatResponse("Sorry, I encountered an error communicating with the AI service.", []);
            }

            // Add all response messages to conversation for potential next iteration
            foreach (var msg in response.Messages)
            {
                messages.Add(msg);
            }

            // Process contents from all response messages
            var functionCalls = new List<FunctionCallContent>();
            foreach (var msg in response.Messages)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is TextContent text && !string.IsNullOrWhiteSpace(text.Text))
                    {
                        responseText.Append(text.Text);
                    }
                    else if (content is FunctionCallContent call)
                    {
                        functionCalls.Add(call);
                    }
                }
            }

            if (functionCalls.Count == 0)
                break;

            // Process tool calls
            var hasReadCalls = false;
            foreach (var call in functionCalls)
            {
                if (WriteTools.Contains(call.Name))
                {
                    // Write tool → proposed action (don't execute)
                    proposedActions.Add(MapToProposedAction(call));
                    // Return a result indicating the action is pending approval
                    messages.Add(new Microsoft.Extensions.AI.ChatMessage(
                        ChatRole.Tool,
                        [new FunctionResultContent(call.CallId, "Action proposed for user confirmation.")]));
                }
                else
                {
                    // Read tool → auto-invoke and feed result back
                    hasReadCalls = true;
                    var result = await ExecuteReadTool(call, ct);
                    messages.Add(new Microsoft.Extensions.AI.ChatMessage(
                        ChatRole.Tool,
                        [new FunctionResultContent(call.CallId, result)]));
                }
            }

            // If there were only write tool calls (no reads), break to let the AI finalize
            if (!hasReadCalls)
            {
                // One more call to get the AI's final text response after proposals
                try
                {
                    var finalResponse = await chatClient.GetResponseAsync(messages, chatOptions, ct);
                    foreach (var msg in finalResponse.Messages)
                    {
                        foreach (var content in msg.Contents)
                        {
                            if (content is TextContent text && !string.IsNullOrWhiteSpace(text.Text))
                            {
                                responseText.Append(text.Text);
                            }
                        }
                    }
                }
                catch (ClientResultException ex) when (IsTokenLimitError(ex))
                {
                    logger.LogWarning("Final AI response hit token limit, using collected text");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to get final AI response after proposals");
                }

                break;
            }

            // Continue loop — AI will process read results and may call more tools
        }

        var finalText = responseText.Length > 0
            ? responseText.ToString()
            : "I've processed your request.";

        return new ChatResponse(finalText, proposedActions);
    }

    public async Task<IReadOnlyList<ActionResult>> ExecuteActionsAsync(
        IReadOnlyList<ProposedAction> actions,
        IReadOnlyList<ActionConfirmation> confirmations,
        CancellationToken ct = default)
    {
        var results = new List<ActionResult>();
        var approvedIndexes = confirmations
            .Where(c => c.Approved)
            .Select(c => c.ActionIndex)
            .ToHashSet();

        for (int i = 0; i < actions.Count; i++)
        {
            if (!approvedIndexes.Contains(i))
                continue;

            var action = actions[i];
            try
            {
                await ExecuteAction(action, ct);
                results.Add(new ActionResult(i, true, $"{action.Type} completed successfully."));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute action {ActionType}", action.Type);
                results.Add(new ActionResult(i, false, $"Failed: {ex.Message}"));
            }
        }

        return results;
    }

    private List<Microsoft.Extensions.AI.ChatMessage> BuildConversation(
        string userMessage,
        IReadOnlyList<ChatMessage> history,
        int maxHistory)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, SystemPrompt)
        };

        // Add capped history, only user/assistant text turns (no tool-call/tool-result noise)
        var historyToInclude = history.Count > maxHistory
            ? history.Skip(history.Count - maxHistory).ToList()
            : history;

        foreach (var msg in historyToInclude)
        {
            var role = msg.Role.ToLowerInvariant() switch
            {
                "user" => ChatRole.User,
                "assistant" => ChatRole.Assistant,
                _ => ChatRole.User
            };
            if (!string.IsNullOrEmpty(msg.Text))
            {
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(role, msg.Text));
            }
        }

        messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, userMessage));
        return messages;
    }

    /// <summary>
    /// Aggressively trims the in-flight conversation for retry after a token limit error.
    /// Keeps system prompt, strips all tool-call/tool-result messages, halves remaining history.
    /// </summary>
    private static List<Microsoft.Extensions.AI.ChatMessage> TrimConversationForRetry(
        List<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        var trimmed = new List<Microsoft.Extensions.AI.ChatMessage>();

        // Keep system prompt
        var systemMsg = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        if (systemMsg is not null)
            trimmed.Add(systemMsg);

        // Keep only user/assistant text messages (drop Tool role and messages with FunctionCallContent)
        var textMessages = messages
            .Where(m => m.Role != ChatRole.System && m.Role != ChatRole.Tool)
            .Where(m => !m.Contents.OfType<FunctionCallContent>().Any())
            .Where(m => m.Contents.OfType<TextContent>().Any(t => !string.IsNullOrWhiteSpace(t.Text)))
            .ToList();

        // Halve the remaining messages, keeping the most recent ones + always the last (current user msg)
        if (textMessages.Count > 4)
        {
            var halfPoint = textMessages.Count / 2;
            textMessages = textMessages.Skip(halfPoint).ToList();
        }

        trimmed.AddRange(textMessages);
        return trimmed;
    }

    /// <summary>
    /// Checks whether the exception represents a token/context limit error (HTTP 413 or similar).
    /// </summary>
    private static bool IsTokenLimitError(ClientResultException ex) =>
        ex.Status == (int)HttpStatusCode.RequestEntityTooLarge
        || (ex.Message is not null && ex.Message.Contains("tokens_limit_reached", StringComparison.OrdinalIgnoreCase));

    private List<AITool> BuildTools()
    {
        return
        [
            AIFunctionFactory.Create(GetTaskListsAsync, "get_task_lists", "Get all task lists for the user."),
            AIFunctionFactory.Create(GetTasksAsync, "get_tasks", "Get tasks in a specific list (title, status, due date). Does not include task description."),
            AIFunctionFactory.Create(GetTodayTasksAsync, "get_today_tasks", "Get tasks due today across all lists (title, status, due date). Does not include task description."),
            AIFunctionFactory.Create(GetTaskDetailAsync, "get_task", "Get full details of a single task including its description. Use this only when the description is specifically needed."),
            AIFunctionFactory.Create(CreateTaskTool, "create_task", "Create a new task in a task list."),
            AIFunctionFactory.Create(CompleteTaskTool, "complete_task", "Mark a task as completed."),
            AIFunctionFactory.Create(UncompleteTaskTool, "uncomplete_task", "Mark a task as not completed."),
        ];
    }

    // Read tool delegates (actual execution for auto-invoke)
    private async Task<string> GetTaskListsAsync()
    {
        var lists = await todoService.GetTaskListsAsync();
        return JsonSerializer.Serialize(lists.Select(l => new { l.Id, l.DisplayName }));
    }

    private async Task<string> GetTasksAsync([Description("The Id field of the task list (opaque API identifier, not the display name)")] string listId)
    {
        var tasks = await todoService.GetTasksAsync(listId);
        return JsonSerializer.Serialize(tasks.Select(t => new { t.Id, t.Title, t.IsCompleted, t.DueDate, t.Importance }));
    }

    private async Task<string> GetTodayTasksAsync()
    {
        var tasks = await todoService.GetTodayTasksAsync();
        return JsonSerializer.Serialize(tasks.Select(t => new { t.Id, t.Title, t.IsCompleted, t.DueDate, t.ListId, t.ListName }));
    }

    private async Task<string> GetTaskDetailAsync(
        [Description("The Id field of the task list (opaque API identifier, not the display name)")] string listId,
        [Description("The Id field of the task (opaque API identifier, not the task title)")] string taskId)
    {
        var task = await todoService.GetTaskAsync(listId, taskId);
        if (task == null)
            return JsonSerializer.Serialize(new { error = "Task not found" });

        return JsonSerializer.Serialize(new { task.Id, task.Title, task.Body, task.IsCompleted, task.DueDate, task.Importance });
    }

    // Write tool stubs (never actually called — only used for schema generation)
    private static string CreateTaskTool(
        [Description("The Id field of the task list (opaque API identifier from get_task_lists, not the display name)")] string listId,
        string title,
        string? dueDate = null) => "proposed";
    private static string CompleteTaskTool(
        [Description("The Id field of the task list (opaque API identifier from get_task_lists, not the display name)")] string listId,
        [Description("The Id field of the task (opaque API identifier from get_tasks/get_today_tasks, not the task title)")] string taskId) => "proposed";
    private static string UncompleteTaskTool(
        [Description("The Id field of the task list (opaque API identifier from get_task_lists, not the display name)")] string listId,
        [Description("The Id field of the task (opaque API identifier from get_tasks/get_today_tasks, not the task title)")] string taskId) => "proposed";

    private async Task<string> ExecuteReadTool(FunctionCallContent call, CancellationToken ct)
    {
        try
        {
            return call.Name switch
            {
                "get_task_lists" => await GetTaskListsAsync(),
                "get_tasks" => await GetTasksAsync(GetArg(call, "listId")),
                "get_task" => await GetTaskDetailAsync(GetArg(call, "listId"), GetArg(call, "taskId")),
                "get_today_tasks" => await GetTodayTasksAsync(),
                _ => $"Unknown tool: {call.Name}"
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Read tool {ToolName} failed", call.Name);
            return $"Error: {ex.Message}";
        }
    }

    private static ProposedAction MapToProposedAction(FunctionCallContent call)
    {
        var (type, description) = call.Name switch
        {
            "create_task" => (TaskActionType.CreateTask,
                $"Create task \"{GetArg(call, "title")}\""),
            "complete_task" => (TaskActionType.CompleteTask,
                $"Complete task {GetArg(call, "taskId")}"),
            "uncomplete_task" => (TaskActionType.UncompleteTask,
                $"Uncomplete task {GetArg(call, "taskId")}"),
            _ => throw new InvalidOperationException($"Unknown write tool: {call.Name}")
        };

        var parameters = new Dictionary<string, string>();
        if (call.Arguments is not null)
        {
            foreach (var kvp in call.Arguments)
            {
                parameters[kvp.Key] = kvp.Value?.ToString() ?? "";
            }
        }

        return new ProposedAction(type, description, parameters);
    }

    private async Task ExecuteAction(ProposedAction action, CancellationToken ct)
    {
        ValidateIdParameter(action, "listId");
        if (action.Type is TaskActionType.CompleteTask or TaskActionType.UncompleteTask)
            ValidateIdParameter(action, "taskId");

        logger.LogDebug("ExecuteAction: {Type}, parameters: {Parameters}",
            action.Type, string.Join(", ", action.Parameters.Select(p => $"{p.Key}={p.Value}")));

        switch (action.Type)
        {
            case TaskActionType.CreateTask:
                DateOnly? dueDate = action.Parameters.TryGetValue("dueDate", out var dueDateStr)
                    && DateOnly.TryParse(dueDateStr, out var parsed)
                    ? parsed
                    : null;
                await todoService.CreateTaskAsync(
                    action.Parameters["listId"],
                    action.Parameters["title"],
                    dueDate);
                break;

            case TaskActionType.CompleteTask:
                await todoService.UpdateTaskStatusAsync(
                    action.Parameters["listId"],
                    action.Parameters["taskId"],
                    completed: true);
                break;

            case TaskActionType.UncompleteTask:
                await todoService.UpdateTaskStatusAsync(
                    action.Parameters["listId"],
                    action.Parameters["taskId"],
                    completed: false);
                break;
        }
    }

    private static string GetArg(FunctionCallContent call, string name)
    {
        if (call.Arguments?.TryGetValue(name, out var value) == true && value is not null)
        {
            // Handle JsonElement values from deserialization
            if (value is JsonElement element)
                return element.GetString() ?? element.ToString();

            return value.ToString() ?? "";
        }

        return "";
    }

    /// <summary>
    /// Validates that a parameter looks like an opaque Graph API ID rather than a display name.
    /// Graph IDs are typically long base64-like strings (30+ chars).
    /// </summary>
    private void ValidateIdParameter(ProposedAction action, string paramName)
    {
        if (!action.Parameters.TryGetValue(paramName, out var value) || string.IsNullOrWhiteSpace(value))
        {
            logger.LogWarning("ExecuteAction: Missing required parameter '{ParamName}' for {ActionType}", paramName, action.Type);
            throw new InvalidOperationException($"Missing required parameter '{paramName}'.");
        }

        // Graph API IDs are long opaque strings; short human-readable values are almost certainly display names
        if (value.Length < 20 && !value.Any(c => c == '=' || c == '-' || c == '_'))
        {
            logger.LogWarning(
                "ExecuteAction: Parameter '{ParamName}' value '{Value}' looks like a display name, not a Graph API ID. " +
                "The AI model should use the Id field from tool responses.",
                paramName, value);
            throw new InvalidOperationException(
                $"Parameter '{paramName}' value '{value}' appears to be a display name instead of a Graph API identifier. " +
                $"Use the Id field from get_task_lists/get_tasks responses.");
        }
    }
}
