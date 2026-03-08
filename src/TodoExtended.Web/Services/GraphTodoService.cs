using System.Globalization;

namespace TodoExtended.Web.Services;

public class GraphTodoService(IGraphTodoClient graphClient, IUserTimeZoneService userTimeZoneService, ILogger<GraphTodoService> logger) : ITodoService
{
    public async Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync()
    {
        var response = await graphClient.GetTaskListsAsync();
        return response
            .Select(l => new TodoTaskList(l.Id!, l.DisplayName ?? "Untitled"))
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId)
    {
        var response = await graphClient.GetTasksAsync(taskListId);

        return response
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
        // "Today" in the user's configured timezone, with boundaries converted to UTC
        // for the Graph API filter. Microsoft To Do stores due dates as midnight
        // local time converted to UTC, so the filter must use UTC equivalents of
        // the local day boundaries.
        var userZone = await userTimeZoneService.GetCurrentUserTimeZoneAsync();
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userZone));
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal.ToDateTime(TimeOnly.MinValue), userZone);
        var tomorrowStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal.AddDays(1).ToDateTime(TimeOnly.MinValue), userZone);
        var filter = $"dueDateTime/dateTime ge '{todayStartUtc:yyyy-MM-ddTHH:mm:ss}' and dueDateTime/dateTime lt '{tomorrowStartUtc:yyyy-MM-ddTHH:mm:ss}'";
        logger.LogDebug("GetTodayTasksAsync: Graph filter='{Filter}'", filter);
        var result = new List<TodoTaskWithList>();

        foreach (var list in lists)
        {
            var response = await graphClient.GetTasksAsync(list.Id, filter);

            foreach (var t in response)
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

    public async Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate, TimeOnly? reminderTime = null)
    {
        var userZone = await userTimeZoneService.GetCurrentUserTimeZoneAsync();
        var newTask = new Microsoft.Graph.Models.TodoTask
        {
            Title = title,
        };

        if (dueDate is { } due)
        {
            newTask.DueDateTime = new Microsoft.Graph.Models.DateTimeTimeZone
            {
                DateTime = due.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = userZone.Id,
            };
        }

        if (reminderTime is { } reminder)
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userZone));
            var reminderDateTime = today.ToDateTime(new TimeOnly(reminder.Hour, reminder.Minute));
            newTask.IsReminderOn = true;
            newTask.ReminderDateTime = new Microsoft.Graph.Models.DateTimeTimeZone
            {
                DateTime = reminderDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = userZone.Id,
            };
        }

        var created = await graphClient.CreateTaskAsync(taskListId, newTask);

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
        await graphClient.PatchTaskAsync(taskListId, taskId, patch);
        logger.LogDebug("UpdateTaskStatusAsync: PatchAsync succeeded for taskId={TaskId}", taskId);
    }

    public Task SetTaskListSyncedAsync(string taskListId, bool isSynced) =>
        throw new NotSupportedException("Syncing task lists is only supported with local cache.");

    public Task<IReadOnlyList<TodoTaskList>> GetNotSyncedTaskListsAsync() =>
        Task.FromResult<IReadOnlyList<TodoTaskList>>([]);

    /// <summary>
    /// Converts Graph's dateTimeTimeZone to a DateOnly.
    /// Microsoft To Do stores due dates as midnight-local-time converted to UTC
    /// (e.g., March 6 00:00 CET → 2026-03-05T23:00:00 UTC). Since the original
    /// value is always midnight in some timezone, adding 12 hours and taking the
    /// date gives the correct result for all practical timezones (UTC-12 to UTC+12).
    /// </summary>
    private DateOnly? ParseDueDate(Microsoft.Graph.Models.DateTimeTimeZone? dueDateTime)
    {
        if (dueDateTime?.DateTime is null) return null;

        logger.LogDebug("ParseDueDate: raw='{DateTime}' timeZone='{TimeZone}'", dueDateTime.DateTime, dueDateTime.TimeZone);

        var dt = DateTime.Parse(dueDateTime.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None);
        var result = DateOnly.FromDateTime(dt.AddHours(12));
        logger.LogDebug("ParseDueDate: result={Result}", result);
        return result;
    }
}

