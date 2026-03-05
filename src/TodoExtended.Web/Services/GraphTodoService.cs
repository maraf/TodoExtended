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
