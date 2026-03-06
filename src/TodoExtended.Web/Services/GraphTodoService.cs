using System.Globalization;
using Microsoft.Graph;

namespace TodoExtended.Web.Services;

public class GraphTodoService(GraphServiceClient graphClient, ILogger<GraphTodoService> logger) : ITodoService
{
    public async Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync()
    {
        var response = await graphClient.Me.Todo.Lists.GetAsync();
        if (response?.Value is null) return [];

        return response.Value
            .Select(l => new TodoTaskList(l.Id!, l.DisplayName ?? "Untitled"))
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId)
    {
        var response = await graphClient.Me.Todo.Lists[taskListId].Tasks.GetAsync();
        if (response?.Value is null) return [];

        return response.Value
            .Select(t =>
            {
                if (t.DueDateTime is not null)
                    logger.LogDebug("GetTasksAsync: Task '{Title}' raw dueDateTime='{DateTime}' timeZone='{TimeZone}'", t.Title, t.DueDateTime.DateTime, t.DueDateTime.TimeZone);

                return new TodoTask(
                    t.Id!,
                    t.Title ?? "Untitled",
                    t.Body?.Content,
                    t.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                    ParseDueDate(t.DueDateTime),
                    t.Importance?.ToString());
            })
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync()
    {
        var lists = await GetTaskListsAsync();
        // "Today" in the user's local timezone, with boundaries converted to UTC
        // for the Graph API filter. Microsoft To Do stores due dates as midnight
        // local time converted to UTC, so the filter must use UTC equivalents of
        // the local day boundaries.
        var todayLocal = DateOnly.FromDateTime(DateTime.Now);
        var todayStartUtc = todayLocal.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        var tomorrowStartUtc = todayLocal.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        var filter = $"dueDateTime/dateTime ge '{todayStartUtc:yyyy-MM-ddTHH:mm:ss}' and dueDateTime/dateTime lt '{tomorrowStartUtc:yyyy-MM-ddTHH:mm:ss}'";
        logger.LogDebug("GetTodayTasksAsync: Graph filter='{Filter}'", filter);
        var result = new List<TodoTaskWithList>();

        foreach (var list in lists)
        {
            var response = await graphClient.Me.Todo.Lists[list.Id].Tasks.GetAsync(config =>
            {
                config.QueryParameters.Filter = filter;
            });
            if (response?.Value is null) continue;

            foreach (var t in response.Value)
            {
                logger.LogDebug("GetTodayTasksAsync: Task '{Title}' raw dueDateTime='{DateTime}' timeZone='{TimeZone}'", t.Title, t.DueDateTime?.DateTime, t.DueDateTime?.TimeZone);

                result.Add(new TodoTaskWithList(
                    t.Id!,
                    t.Title ?? "Untitled",
                    t.Body?.Content,
                    t.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                    ParseDueDate(t.DueDateTime),
                    t.Importance?.ToString(),
                    list.Id,
                    list.DisplayName));
            }
        }

        return result;
    }

    public async Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate)
    {
        var newTask = new Microsoft.Graph.Models.TodoTask
        {
            Title = title,
        };

        if (dueDate is { } due)
        {
            newTask.DueDateTime = new Microsoft.Graph.Models.DateTimeTimeZone
            {
                DateTime = due.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = "UTC",
            };
        }

        var created = await graphClient.Me.Todo.Lists[taskListId].Tasks.PostAsync(newTask)
            ?? throw new InvalidOperationException("Graph API returned null when creating task.");

        return new TodoTask(
            created.Id!,
            created.Title ?? title,
            created.Body?.Content,
            created.Status == Microsoft.Graph.Models.TaskStatus.Completed,
            ParseDueDate(created.DueDateTime),
            created.Importance?.ToString());
    }

    public async Task UpdateTaskStatusAsync(string taskListId, string taskId, bool completed)
    {
        logger.LogDebug("UpdateTaskStatusAsync: taskListId={TaskListId}, taskId={TaskId}, completed={Completed}", taskListId, taskId, completed);

        var patch = new Microsoft.Graph.Models.TodoTask
        {
            Status = completed
                ? Microsoft.Graph.Models.TaskStatus.Completed
                : Microsoft.Graph.Models.TaskStatus.NotStarted,
        };

        logger.LogDebug("UpdateTaskStatusAsync: Sending PatchAsync for taskId={TaskId}, status={Status}", taskId, patch.Status);
        await graphClient.Me.Todo.Lists[taskListId].Tasks[taskId].PatchAsync(patch);
        logger.LogDebug("UpdateTaskStatusAsync: PatchAsync succeeded for taskId={TaskId}", taskId);
    }

    public Task SetTaskListArchivedAsync(string taskListId, bool isArchived) =>
        throw new NotSupportedException("Archiving task lists is only supported with local cache.");

    public Task<IReadOnlyList<TodoTaskList>> GetArchivedTaskListsAsync() =>
        Task.FromResult<IReadOnlyList<TodoTaskList>>([]);

    /// <summary>
    /// Converts Graph's dateTimeTimeZone to a local DateOnly.
    /// Microsoft To Do stores due dates as midnight-local-time converted to UTC
    /// (e.g., March 6 00:00 CET is stored as 2026-03-05T23:00:00 UTC). We must
    /// convert back to local time before extracting the date.
    /// </summary>
    private DateOnly? ParseDueDate(Microsoft.Graph.Models.DateTimeTimeZone? dueDateTime)
    {
        if (dueDateTime?.DateTime is null) return null;

        logger.LogDebug("ParseDueDate: raw='{DateTime}' timeZone='{TimeZone}'", dueDateTime.DateTime, dueDateTime.TimeZone);

        // Parse the full datetime and convert from the source timezone to local.
        // Microsoft To Do stores due dates as midnight-local converted to UTC,
        // so extracting just the UTC date gives the wrong day.
        var dt = DateTime.Parse(dueDateTime.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None);

        if (!string.IsNullOrEmpty(dueDateTime.TimeZone))
        {
            var sourceZone = TimeZoneInfo.FindSystemTimeZoneById(dueDateTime.TimeZone);
            dt = TimeZoneInfo.ConvertTime(dt, sourceZone, TimeZoneInfo.Local);
        }

        var result = DateOnly.FromDateTime(dt);
        logger.LogDebug("ParseDueDate: result={Result}", result);
        return result;
    }
}
