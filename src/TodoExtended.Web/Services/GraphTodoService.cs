using System.Globalization;

namespace TodoExtended.Web.Services;

public class GraphTodoService(IGraphTodoClient graphClient, IUserTimeZoneService userTimeZoneService, ILogger<GraphTodoService> logger) : ITodoService
{
    public async Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync(string userId)
    {
        var response = await graphClient.GetTaskListsAsync();
        return response
            .Select(l => new TodoTaskList(l.Id!, l.DisplayName ?? "Untitled"))
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId, string userId)
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
                    t.Importance?.ToString(),
                    t.IsReminderOn == true,
                    t.Recurrence != null);
            })
            .ToList();
    }

    public async Task<TodoTask?> GetTaskAsync(string taskListId, string taskId, string userId)
    {
        var t = await graphClient.GetTaskAsync(taskListId, taskId);
        if (t == null)
            return null;

        return new TodoTask(
            t.Id!,
            t.Title ?? "Untitled",
            t.Body?.Content,
            t.Status == Microsoft.Graph.Models.TaskStatus.Completed,
            ParseDueDate(t.DueDateTime),
            t.Importance?.ToString(),
            t.IsReminderOn == true,
            t.Recurrence != null);
    }

    public Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync(string userId) =>
        GetTasksForDayOffsetAsync(userId, dayOffset: 0);

    public Task<IReadOnlyList<TodoTaskWithList>> GetTomorrowTasksAsync(string userId) =>
        GetTasksForDayOffsetAsync(userId, dayOffset: 1);

    private async Task<IReadOnlyList<TodoTaskWithList>> GetTasksForDayOffsetAsync(string userId, int dayOffset)
    {
        var lists = await GetTaskListsAsync(userId);
        // Build UTC boundaries for the target day in the user's timezone.
        // Microsoft To Do stores due dates as midnight local time converted to UTC,
        // so the filter must use UTC equivalents of the local day boundaries.
        var userZone = await userTimeZoneService.GetCurrentUserTimeZoneAsync();
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userZone));
        var dayStartLocal = todayLocal.AddDays(dayOffset);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal.ToDateTime(TimeOnly.MinValue), userZone);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal.AddDays(1).ToDateTime(TimeOnly.MinValue), userZone);
        var filter = $"dueDateTime/dateTime ge '{dayStartUtc:yyyy-MM-ddTHH:mm:ss}' and dueDateTime/dateTime lt '{dayEndUtc:yyyy-MM-ddTHH:mm:ss}'";
        var callerName = dayOffset == 0 ? "GetTodayTasksAsync" : "GetTomorrowTasksAsync";
        logger.LogDebug("{Caller}: Graph filter='{Filter}'", callerName, filter);
        return await GetTasksForDayFilterAsync(lists, filter, callerName);
    }

    private async Task<List<TodoTaskWithList>> GetTasksForDayFilterAsync(IReadOnlyList<TodoTaskList> lists, string filter, string callerName)
    {
        var result = new List<TodoTaskWithList>();
        foreach (var list in lists)
        {
            var response = await graphClient.GetTasksAsync(list.Id, filter);
            foreach (var t in response)
            {
                logger.LogDebug("{Caller}: Task '{Title}' raw dueDateTime='{DateTime}' timeZone='{TimeZone}'", callerName, t.Title, t.DueDateTime?.DateTime, t.DueDateTime?.TimeZone);
                result.Add(new TodoTaskWithList(
                    t.Id!,
                    t.Title ?? "Untitled",
                    t.Body?.Content,
                    t.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                    ParseDueDate(t.DueDateTime),
                    t.Importance?.ToString(),
                    list.Id,
                    list.DisplayName,
                    t.IsReminderOn == true,
                    t.Recurrence != null));
            }
        }
        return result;
    }

    public async Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate, string userId, TimeOnly? reminderTime = null)
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
            created.Importance?.ToString(),
            created.IsReminderOn == true,
            created.Recurrence != null);
    }

    public async Task UpdateTaskStatusAsync(string taskListId, string taskId, bool completed, string userId)
    {
        logger.LogDebug("UpdateTaskStatusAsync: taskListId={TaskListId}, taskId={TaskId}, completed={Completed}", taskListId, taskId, completed);

        var patch = new Microsoft.Graph.Models.TodoTask
        {
            Status = completed
                ? Microsoft.Graph.Models.TaskStatus.Completed
                : Microsoft.Graph.Models.TaskStatus.NotStarted,
        };

        logger.LogDebug("UpdateTaskStatusAsync: Sending PatchAsync for taskId={TaskId}, status={Status}", taskId, patch.Status);

        try
        {
            await graphClient.PatchTaskAsync(taskListId, taskId, patch);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            logger.LogError(ex,
                "UpdateTaskStatusAsync: ODataError Code={Code}, Message={Message}, StatusCode={StatusCode}",
                ex.Error?.Code, ex.Error?.Message, ex.ResponseStatusCode);

            if (ex.Error?.Details is { Count: > 0 } details)
            {
                foreach (var detail in details)
                    logger.LogError("  ODataError Detail: Code={Code}, Message={Message}", detail.Code, detail.Message);
            }

            throw;
        }

        logger.LogDebug("UpdateTaskStatusAsync: PatchAsync succeeded for taskId={TaskId}", taskId);
    }

    public async Task SetTaskDueDateAsync(string taskListId, string taskId, DateOnly dueDate, string userId)
    {
        logger.LogDebug("SetTaskDueDateAsync: taskListId={TaskListId}, taskId={TaskId}, dueDate={DueDate}", taskListId, taskId, dueDate);

        var userZone = await userTimeZoneService.GetCurrentUserTimeZoneAsync();

        var patch = new Microsoft.Graph.Models.TodoTask
        {
            DueDateTime = new Microsoft.Graph.Models.DateTimeTimeZone
            {
                DateTime = dueDate.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                TimeZone = userZone.Id,
            },
        };

        try
        {
            // Fetch the current task to check for an active reminder and recurrence.
            var current = await graphClient.GetTaskAsync(taskListId, taskId);

            // If the task has an active reminder, reschedule it to the same time on the new due date.
            if (current?.IsReminderOn == true && current.ReminderDateTime?.DateTime is not null)
            {
                var currentReminderDt = DateTime.Parse(current.ReminderDateTime.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None);

                // Graph may return ReminderDateTime in a different timezone than the user's (e.g. UTC).
                // Convert from the returned timezone to the user's local timezone so the clock time is preserved.
                var reminderTime = ConvertReminderToUserLocalTime(currentReminderDt, current.ReminderDateTime.TimeZone, userZone);

                var newReminderDt = dueDate.ToDateTime(reminderTime);
                patch.IsReminderOn = true;
                patch.ReminderDateTime = new Microsoft.Graph.Models.DateTimeTimeZone
                {
                    DateTime = newReminderDt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                    TimeZone = userZone.Id,
                };
                logger.LogDebug("SetTaskDueDateAsync: Rescheduling reminder from {Old} (tz={OldTz}) to {New} (tz={NewTz})", currentReminderDt, current.ReminderDateTime.TimeZone, newReminderDt, userZone.Id);
            }

            // If the task is recurring, update the recurrence range startDate to match the new due date.
            // Without this, Microsoft To Do creates a duplicate occurrence for the original date when
            // the dueDateTime is patched.
            if (current?.Recurrence?.Range != null)
            {
                // Build a fresh RecurrencePattern instead of reusing the deserialized one.
                // The Kiota backing store on the GET response object may have null AdditionalData,
                // which causes "AdditionalData can not be null" during PATCH serialization.
                var src = current.Recurrence.Pattern;
                patch.Recurrence = new Microsoft.Graph.Models.PatternedRecurrence
                {
                    Pattern = src == null ? null : new Microsoft.Graph.Models.RecurrencePattern
                    {
                        Type = src.Type,
                        Interval = src.Interval,
                        Month = src.Month,
                        DayOfMonth = src.DayOfMonth,
                        DaysOfWeek = src.DaysOfWeek,
                        FirstDayOfWeek = src.FirstDayOfWeek,
                        Index = src.Index,
                    },
                    Range = new Microsoft.Graph.Models.RecurrenceRange
                    {
                        Type = current.Recurrence.Range.Type,
                        StartDate = new Microsoft.Kiota.Abstractions.Date(dueDate.Year, dueDate.Month, dueDate.Day),
                        // Only copy EndDate when the range type is EndDate; for NoEnd/Numbered
                        // ranges the Graph API returns a default 0001-01-01 value which is invalid
                        // for OData serialization.
                        EndDate = current.Recurrence.Range.Type == Microsoft.Graph.Models.RecurrenceRangeType.EndDate
                            ? current.Recurrence.Range.EndDate
                            : null,
                        NumberOfOccurrences = current.Recurrence.Range.NumberOfOccurrences,
                        RecurrenceTimeZone = current.Recurrence.Range.RecurrenceTimeZone,
                    },
                };
                logger.LogDebug("SetTaskDueDateAsync: Updating recurrence startDate to {StartDate} for recurring task {TaskId}", dueDate, taskId);
            }

            await graphClient.PatchTaskAsync(taskListId, taskId, patch);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            logger.LogError(ex,
                "SetTaskDueDateAsync: ODataError Code={Code}, Message={Message}, StatusCode={StatusCode}",
                ex.Error?.Code, ex.Error?.Message, ex.ResponseStatusCode);

            if (ex.Error?.Details is { Count: > 0 } details)
            {
                foreach (var detail in details)
                    logger.LogError("  ODataError Detail: Code={Code}, Message={Message}", detail.Code, detail.Message);
            }

            throw;
        }

        logger.LogDebug("SetTaskDueDateAsync: PatchAsync succeeded for taskId={TaskId}", taskId);
    }

    public async Task SetTaskReminderAsync(string taskListId, string taskId, DateOnly reminderDate, TimeOnly reminderTime, string userId)
    {
        logger.LogDebug("SetTaskReminderAsync: taskListId={TaskListId}, taskId={TaskId}, reminderDate={ReminderDate}, reminderTime={ReminderTime}", taskListId, taskId, reminderDate, reminderTime);

        var userZone = await userTimeZoneService.GetCurrentUserTimeZoneAsync();
        var reminderDateTime = reminderDate.ToDateTime(reminderTime);

        var patch = new Microsoft.Graph.Models.TodoTask
        {
            IsReminderOn = true,
            ReminderDateTime = new Microsoft.Graph.Models.DateTimeTimeZone
            {
                DateTime = reminderDateTime.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                TimeZone = userZone.Id,
            },
        };

        try
        {
            await graphClient.PatchTaskAsync(taskListId, taskId, patch);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            logger.LogError(ex,
                "SetTaskReminderAsync: ODataError Code={Code}, Message={Message}, StatusCode={StatusCode}",
                ex.Error?.Code, ex.Error?.Message, ex.ResponseStatusCode);

            if (ex.Error?.Details is { Count: > 0 } details)
            {
                foreach (var detail in details)
                    logger.LogError("  ODataError Detail: Code={Code}, Message={Message}", detail.Code, detail.Message);
            }

            throw;
        }

        logger.LogDebug("SetTaskReminderAsync: PatchAsync succeeded for taskId={TaskId}", taskId);
    }

    public Task SetTaskListSyncedAsync(string taskListId, bool isSynced, string userId) =>
        throw new NotSupportedException("Syncing task lists is only supported with local cache.");

    public Task<IReadOnlyList<TodoTaskList>> GetNotSyncedTaskListsAsync(string userId) =>
        Task.FromResult<IReadOnlyList<TodoTaskList>>([]);

    public async Task<IReadOnlyList<TodoTaskWithList>> SearchTasksAsync(string query, string userId)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > MaxSearchQueryLength)
            return [];

        var lists = await GetTaskListsAsync(userId);
        var listTasks = await Task.WhenAll(lists.Select(async list =>
        {
            var tasks = await GetTasksAsync(list.Id, userId);
            return (list, tasks);
        }));

        return listTasks
            .SelectMany(lt => lt.tasks
                .Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(t => new TodoTaskWithList(
                    t.Id, t.Title, t.Body, t.IsCompleted, t.DueDate, t.Importance,
                    lt.list.Id, lt.list.DisplayName, t.HasReminder, t.IsRecurring)))
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private const int MaxSearchQueryLength = 500;

    public async Task<IReadOnlyList<TodoTaskList>> SearchTaskListsAsync(string query, string userId)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > MaxSearchQueryLength)
            return [];

        var lists = await GetTaskListsAsync(userId);
        return lists
            .Where(l => l.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Converts a reminder datetime returned by Graph (which may be in a different timezone, e.g. UTC)
    /// to the equivalent local time in <paramref name="userZone"/>, so the user's intended clock time
    /// is preserved when rescheduling.
    /// </summary>
    private static TimeOnly ConvertReminderToUserLocalTime(DateTime reminderDt, string? reminderTzId, TimeZoneInfo userZone)
    {
        if (!string.IsNullOrEmpty(reminderTzId))
        {
            try
            {
                var reminderTz = TimeZoneInfo.FindSystemTimeZoneById(reminderTzId);
                var utcDt = TimeZoneInfo.ConvertTimeToUtc(reminderDt, reminderTz);
                var userLocalDt = TimeZoneInfo.ConvertTimeFromUtc(utcDt, userZone);
                return TimeOnly.FromDateTime(userLocalDt);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
            {
                // Fall back: treat the datetime as already in the user's timezone.
            }
        }

        return TimeOnly.FromDateTime(reminderDt);
    }

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

