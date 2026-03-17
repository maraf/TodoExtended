using System.ClientModel;
using System.ComponentModel;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using TodoExtended.Web.Extensions;
using TodoExtended.Web.Services;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services.AiChat;

/// <summary>
/// Real AI chat implementation using Microsoft.Extensions.AI with tool-calling.
/// Read tools are auto-invoked; write tools become proposed actions for user confirmation.
/// </summary>
public class ChatService(
    IChatClient chatClient,
    ITodoService todoService,
    ITemplateService templateService,
    IHttpContextAccessor httpContextAccessor,
    IUserTimeZoneService userTimeZoneService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<AiChatOptions> options,
    ILogger<ChatService> logger) : IChatService
{
    private static readonly HashSet<string> WriteTools = ["create_task", "complete_task", "uncomplete_task", "set_task_reminder", "create_template", "update_template", "delete_template", "execute_template"];

    private string GetCurrentUserId() =>
        httpContextAccessor.HttpContext?.User.GetUserIdOrNull()
        ?? throw new InvalidOperationException("User not authenticated");

    private const string SystemPrompt = """
        You are a helpful task management assistant for the TodoExtended app.
        You help users manage their Microsoft To Do tasks and task templates using the available tools.

        Available capabilities:
        - View task lists and their tasks
        - View today's tasks across all lists
        - Search tasks by keyword across all synced lists
        - Search task lists by name
        - Get full details of a specific task (including description)
        - Create new tasks in specific lists
        - Mark tasks as complete or incomplete
        - Set reminders on existing tasks
        - View task templates
        - Create new templates (requires task list info and title)
        - Update existing templates
        - Delete templates
        - Execute templates (creates a task from a template)
        - Get the current date and time in the user's timezone

        Guidelines:
        - Use the read tools to fetch current data before answering questions about tasks or templates.
        - Use get_task_lists to discover lists, get_tasks or get_today_tasks to get task titles and IDs.
        - Use search_tasks to find tasks matching a keyword across all synced lists.
        - Use search_task_lists to find lists matching a name keyword.
        - Use get_task to load full details (including description) for a specific task only when needed.
        - Use get_templates to view available templates.
        - When setting a reminder, use get_tasks or search_tasks first to obtain the task ID. Ask the user for the reminder date and time if not specified; default the date to today if omitted.
        - When creating tasks or templates, always confirm which list to add them to.
        - Be concise and helpful.
        - Format task and template information clearly.
        - When listing tasks, include their completion status and due dates if available.

        CRITICAL: The listId and taskId parameters must be the exact "Id" field values
        returned by get_task_lists, get_tasks, get_today_tasks, or get_task.
        These are opaque API identifiers (e.g. "AQMkADAwATM0MDAAMS0..."), NOT display names.
        Template IDs are GUIDs (e.g. "f47ac10b-58cc-4372-a567-0e02b2c3d479").
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
        var referencedLists = new List<TaskListReference>(); // ordered, as returned by tool
        var referencedListIds = new HashSet<string>();       // for de-duplication

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
                    var proposed = MapToProposedAction(call);
                    await EnrichListNameAsync(proposed, ct);
                    if (call.Name == "set_task_reminder")
                        await EnrichSetReminderActionAsync(proposed, ct);
                    proposedActions.Add(proposed);
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
                    if (call.Name == "get_task_lists")
                        ParseTaskListReferences(result, referencedLists, referencedListIds);
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

        var taskListRefs = referencedLists.Count > 0 ? referencedLists : null;

        return new ChatResponse(finalText, proposedActions, taskListRefs);
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

    /// <summary>
    /// Parses a get_task_lists JSON result and populates the tracker dictionary (Id → DisplayName).
    /// </summary>
    private static void ParseTaskListReferences(string json, List<TaskListReference> tracker, HashSet<string> seenIds)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("Id", out var idEl) && item.TryGetProperty("DisplayName", out var dnEl))
                {
                    var id = idEl.GetString();
                    var dn = dnEl.GetString();
                    if (id is not null && dn is not null && seenIds.Add(id))
                        tracker.Add(new TaskListReference(id, dn));
                }
            }
        }
        catch { /* ignore parse errors */ }
    }

    private List<AITool> BuildTools()
    {
        return
        [
            AIFunctionFactory.Create(GetCurrentDateTimeAsync, "get_current_datetime", "Get the current date and time in the user's local timezone."),
            AIFunctionFactory.Create(GetTaskListsAsync, "get_task_lists", "Get all task lists for the user."),
            AIFunctionFactory.Create(GetTasksAsync, "get_tasks", "Get tasks in a specific list (title, status, due date). Does not include task description."),
            AIFunctionFactory.Create(GetTodayTasksAsync, "get_today_tasks", "Get tasks due today across all lists (title, status, due date). Does not include task description."),
            AIFunctionFactory.Create(SearchTasksAsync, "search_tasks", "Search tasks by keyword across all synced lists. Returns matching tasks with their list info."),
            AIFunctionFactory.Create(SearchTaskListsAsync, "search_task_lists", "Search task lists by name keyword. Returns matching lists."),
            AIFunctionFactory.Create(GetTaskDetailAsync, "get_task", "Get full details of a single task including its description. Use this only when the description is specifically needed."),
            AIFunctionFactory.Create(GetTemplatesAsync, "get_templates", "Get all task templates."),
            AIFunctionFactory.Create(CreateTaskTool, "create_task", "Create a new task in a task list."),
            AIFunctionFactory.Create(CompleteTaskTool, "complete_task", "Mark a task as completed."),
            AIFunctionFactory.Create(UncompleteTaskTool, "uncomplete_task", "Mark a task as not completed."),
            AIFunctionFactory.Create(SetTaskReminderTool, "set_task_reminder", "Set a reminder on an existing task."),
            AIFunctionFactory.Create(CreateTemplateTool, "create_template", "Create a new task template."),
            AIFunctionFactory.Create(UpdateTemplateTool, "update_template", "Update an existing task template."),
            AIFunctionFactory.Create(DeleteTemplateTool, "delete_template", "Delete a task template."),
            AIFunctionFactory.Create(ExecuteTemplateTool, "execute_template", "Execute a template to create a task."),
        ];
    }

    // Read tool delegates (actual execution for auto-invoke)
    private async Task<string> GetCurrentDateTimeAsync()
    {
        var tz = await userTimeZoneService.GetCurrentUserTimeZoneAsync();
        var utcNow = DateTime.UtcNow;
        var offset = tz.GetUtcOffset(utcNow);
        var now = new DateTimeOffset(TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz), offset);
        return JsonSerializer.Serialize(new
        {
            DateTimeOffset = now.ToString("O"),
            TimeZoneId = tz.Id,
            UtcOffsetMinutes = (int)offset.TotalMinutes,
        });
    }

    private async Task<string> GetTaskListsAsync()
    {
        var userId = GetCurrentUserId();
        var lists = await todoService.GetTaskListsAsync(userId);
        return JsonSerializer.Serialize(lists.Select(l => new { l.Id, l.DisplayName }));
    }

    private async Task<string> GetTasksAsync([Description("The Id field of the task list (opaque API identifier, not the display name)")] string listId)
    {
        var userId = GetCurrentUserId();
        var tasks = await todoService.GetTasksAsync(listId, userId);
        return JsonSerializer.Serialize(tasks.Select(t => new { t.Id, t.Title, t.IsCompleted, t.DueDate, t.Importance }));
    }

    private async Task<string> GetTodayTasksAsync()
    {
        var userId = GetCurrentUserId();
        var tasks = await todoService.GetTodayTasksAsync(userId);
        return JsonSerializer.Serialize(tasks.Select(t => new { t.Id, t.Title, t.IsCompleted, t.DueDate, t.ListId, t.ListName }));
    }

    private async Task<string> GetTaskDetailAsync(
        [Description("The Id field of the task list (opaque API identifier, not the display name)")] string listId,
        [Description("The Id field of the task (opaque API identifier, not the task title)")] string taskId)
    {
        var userId = GetCurrentUserId();
        var task = await todoService.GetTaskAsync(listId, taskId, userId);
        if (task == null)
            return JsonSerializer.Serialize(new { error = "Task not found" });

        return JsonSerializer.Serialize(new { task.Id, task.Title, task.Body, task.IsCompleted, task.DueDate, task.Importance });
    }

    private async Task<string> GetTemplatesAsync()
    {
        var userId = GetCurrentUserId();
        var templates = await templateService.GetAllAsync(userId);
        return JsonSerializer.Serialize(templates.Select(t => new { t.Id, t.Title, t.TaskListId, t.TaskListName, t.DueDateToday, t.ReminderTime, t.SortOrder }));
    }

    private async Task<string> SearchTasksAsync(
        [Description("Keyword to search for in task titles (case-insensitive substring match)")] string query)
    {
        var userId = GetCurrentUserId();
        var tasks = await todoService.SearchTasksAsync(query, userId);
        return JsonSerializer.Serialize(tasks.Select(t => new { t.Id, t.Title, t.IsCompleted, t.DueDate, t.Importance, t.ListId, t.ListName }));
    }

    private async Task<string> SearchTaskListsAsync(
        [Description("Keyword to search for in task list names (case-insensitive substring match)")] string query)
    {
        var userId = GetCurrentUserId();
        var lists = await todoService.SearchTaskListsAsync(query, userId);
        return JsonSerializer.Serialize(lists.Select(l => new { l.Id, l.DisplayName }));
    }

    // Write tool stubs (never actually called — only used for schema generation)
    private static string CreateTaskTool(
        [Description("The Id field of the task list (opaque API identifier from get_task_lists, not the display name)")] string listId,
        string title,
        string? dueDate = null,
        [Description("The display name of the task list from get_task_lists")] string? listName = null) => "proposed";
    private static string CompleteTaskTool(
        [Description("The Id field of the task list (opaque API identifier from get_task_lists, not the display name)")] string listId,
        [Description("The Id field of the task (opaque API identifier from get_tasks/get_today_tasks, not the task title)")] string taskId,
        [Description("The display title of the task from get_tasks or get_today_tasks")] string? taskTitle = null,
        [Description("The display name of the task list from get_task_lists")] string? listName = null) => "proposed";
    private static string UncompleteTaskTool(
        [Description("The Id field of the task list (opaque API identifier from get_task_lists, not the display name)")] string listId,
        [Description("The Id field of the task (opaque API identifier from get_tasks/get_today_tasks, not the task title)")] string taskId,
        [Description("The display title of the task from get_tasks or get_today_tasks")] string? taskTitle = null,
        [Description("The display name of the task list from get_task_lists")] string? listName = null) => "proposed";
    private static string SetTaskReminderTool(
        [Description("The Id field of the task list (opaque API identifier from get_task_lists, not the display name)")] string listId,
        [Description("The Id field of the task (opaque API identifier from get_tasks/get_today_tasks, not the task title)")] string taskId,
        [Description("Reminder time in HH:mm format (e.g. 09:00)")] string reminderTime,
        [Description("Reminder date in yyyy-MM-dd format; defaults to today if omitted")] string? reminderDate = null,
        [Description("The display title of the task from get_tasks or get_today_tasks")] string? taskTitle = null,
        [Description("The display name of the task list from get_task_lists")] string? listName = null) => "proposed";
    private static string CreateTemplateTool(
        string title,
        [Description("The Id field of the task list (opaque API identifier from get_task_lists)")] string listId,
        [Description("The display name of the task list from get_task_lists")] string listName,
        bool dueDateToday = false,
        [Description("Reminder time in HH:mm format (e.g. 09:00)")] string? reminderTime = null) => "proposed";
    private static string UpdateTemplateTool(
        [Description("The template Id (GUID)")] string templateId,
        string? title = null,
        [Description("The Id field of the task list (opaque API identifier from get_task_lists)")] string? listId = null,
        [Description("The display name of the task list from get_task_lists")] string? listName = null,
        bool? dueDateToday = null,
        [Description("Reminder time in HH:mm format (e.g. 09:00)")] string? reminderTime = null) => "proposed";
    private static string DeleteTemplateTool(
        [Description("The template Id (GUID)")] string templateId) => "proposed";
    private static string ExecuteTemplateTool(
        [Description("The template Id (GUID)")] string templateId) => "proposed";

    private async Task<string> ExecuteReadTool(FunctionCallContent call, CancellationToken ct)
    {
        try
        {
            return call.Name switch
            {
                "get_current_datetime" => await GetCurrentDateTimeAsync(),
                "get_task_lists" => await GetTaskListsAsync(),
                "get_tasks" => await GetTasksAsync(GetArg(call, "listId")),
                "get_task" => await GetTaskDetailAsync(GetArg(call, "listId"), GetArg(call, "taskId")),
                "get_today_tasks" => await GetTodayTasksAsync(),
                "get_templates" => await GetTemplatesAsync(),
                "search_tasks" => await SearchTasksAsync(GetArg(call, "query")),
                "search_task_lists" => await SearchTaskListsAsync(GetArg(call, "query")),
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
            "set_task_reminder" => (TaskActionType.SetReminder,
                GetArg(call, "taskTitle") is { Length: > 0 } tt
                    ? $"Set reminder on task \"{tt}\" at {GetArg(call, "reminderTime")}"
                    : $"Set reminder on task {GetArg(call, "taskId")} at {GetArg(call, "reminderTime")}"),
            "create_template" => (TaskActionType.CreateTemplate,
                $"Create template \"{GetArg(call, "title")}\""),
            "update_template" => (TaskActionType.UpdateTemplate,
                $"Update template {GetArg(call, "templateId")}"),
            "delete_template" => (TaskActionType.DeleteTemplate,
                $"Delete template {GetArg(call, "templateId")}"),
            "execute_template" => (TaskActionType.ExecuteTemplate,
                $"Execute template {GetArg(call, "templateId")}"),
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
        // Validate Graph API IDs (listId, taskId) — template IDs are GUIDs and don't need this validation
        var isTemplateAction = action.Type is TaskActionType.CreateTemplate or TaskActionType.UpdateTemplate 
            or TaskActionType.DeleteTemplate or TaskActionType.ExecuteTemplate;

        if (!isTemplateAction && action.Parameters.ContainsKey("listId"))
            ValidateIdParameter(action, "listId");
        
        if (action.Type is TaskActionType.CompleteTask or TaskActionType.UncompleteTask or TaskActionType.SetReminder)
            ValidateIdParameter(action, "taskId");

        logger.LogDebug("ExecuteAction: {Type}, parameters: {Parameters}",
            action.Type, string.Join(", ", action.Parameters.Select(p => $"{p.Key}={p.Value}")));

        var userId = GetCurrentUserId();

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
                    dueDate,
                    userId);
                break;

            case TaskActionType.CompleteTask:
                await todoService.UpdateTaskStatusAsync(
                    action.Parameters["listId"],
                    action.Parameters["taskId"],
                    completed: true,
                    userId);
                break;

            case TaskActionType.UncompleteTask:
                await todoService.UpdateTaskStatusAsync(
                    action.Parameters["listId"],
                    action.Parameters["taskId"],
                    completed: false,
                    userId);
                break;

            case TaskActionType.SetReminder:
                if (!TimeOnly.TryParseExact(action.Parameters.GetValueOrDefault("reminderTime"), ["HH:mm", "H:mm"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var setReminderTime))
                    throw new InvalidOperationException("Missing or invalid reminderTime parameter (expected HH:mm).");
                DateOnly setReminderDate;
                if (action.Parameters.TryGetValue("reminderDate", out var reminderDateStr) && !string.IsNullOrEmpty(reminderDateStr))
                {
                    if (!DateOnly.TryParseExact(reminderDateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out setReminderDate))
                        throw new InvalidOperationException("Invalid reminderDate parameter (expected yyyy-MM-dd).");
                }
                else
                {
                    setReminderDate = await userTimeZoneService.GetTodayAsync();
                }
                await todoService.SetTaskReminderAsync(
                    action.Parameters["listId"],
                    action.Parameters["taskId"],
                    setReminderDate,
                    setReminderTime,
                    userId);
                break;

            case TaskActionType.CreateTemplate:
                TimeOnly? reminderTime = action.Parameters.TryGetValue("reminderTime", out var reminderStr)
                    && TimeOnly.TryParse(reminderStr, out var parsedTime)
                    ? parsedTime
                    : null;
                bool dueDateToday = action.Parameters.TryGetValue("dueDateToday", out var dueDateTodayStr)
                    && bool.TryParse(dueDateTodayStr, out var parsedDueDateToday)
                    && parsedDueDateToday;
                await templateService.CreateAsync(new TaskTemplate
                {
                    Title = action.Parameters["title"],
                    TaskListId = action.Parameters["listId"],
                    TaskListName = action.Parameters["listName"],
                    DueDateToday = dueDateToday,
                    ReminderTime = reminderTime,
                    UserId = userId
                }, userId);
                break;

            case TaskActionType.UpdateTemplate:
                var templateId = Guid.Parse(action.Parameters["templateId"]);
                var template = await templateService.GetByIdAsync(templateId, userId);
                if (template == null)
                    throw new InvalidOperationException($"Template {templateId} not found.");

                if (action.Parameters.TryGetValue("title", out var title))
                    template.Title = title;
                if (action.Parameters.TryGetValue("listId", out var listId))
                    template.TaskListId = listId;
                if (action.Parameters.TryGetValue("listName", out var listName))
                    template.TaskListName = listName;
                if (action.Parameters.TryGetValue("dueDateToday", out var dueDateTodayUpdateStr)
                    && bool.TryParse(dueDateTodayUpdateStr, out var parsedDueDateTodayUpdate))
                    template.DueDateToday = parsedDueDateTodayUpdate;
                if (action.Parameters.TryGetValue("reminderTime", out var reminderUpdateStr))
                {
                    template.ReminderTime = TimeOnly.TryParse(reminderUpdateStr, out var parsedReminderUpdate)
                        ? parsedReminderUpdate
                        : null;
                }

                await templateService.UpdateAsync(template, userId);
                break;

            case TaskActionType.DeleteTemplate:
                var deleteTemplateId = Guid.Parse(action.Parameters["templateId"]);
                await templateService.DeleteAsync(deleteTemplateId, userId);
                break;

            case TaskActionType.ExecuteTemplate:
                var executeTemplateId = Guid.Parse(action.Parameters["templateId"]);
                await templateService.ExecuteTemplateAsync(executeTemplateId, userId);
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
    /// Resolves the task list display name from cache for any proposed action that carries
    /// a <c>listId</c> but is missing <c>listName</c>.
    /// </summary>
    private async Task EnrichListNameAsync(ProposedAction action, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(action.Parameters.GetValueOrDefault("listName"))
            && action.Parameters.TryGetValue("listId", out var listId)
            && !string.IsNullOrEmpty(listId))
        {
            var userId = GetCurrentUserId();
            await using var db = await dbContextFactory.CreateDbContextAsync(ct);
            var displayName = await db.CachedTaskLists
                .Where(l => l.Id == listId && l.UserId == userId)
                .Select(l => l.DisplayName)
                .FirstOrDefaultAsync(ct);
            if (displayName is not null)
                action.Parameters["listName"] = displayName;
        }
    }

    /// <summary>
    /// Enriches a SetReminder proposed action with a resolved reminder date
    /// (defaults to today when not provided by the AI).
    /// </summary>
    private async Task EnrichSetReminderActionAsync(ProposedAction action, CancellationToken ct)
    {
        // Default reminderDate to today (in the user's time zone) if not provided
        if (!action.Parameters.TryGetValue("reminderDate", out var rd) || string.IsNullOrEmpty(rd))
        {
            var today = await userTimeZoneService.GetTodayAsync();
            action.Parameters["reminderDate"] = today.ToString("yyyy-MM-dd");
        }
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
