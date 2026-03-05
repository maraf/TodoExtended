using System.Globalization;
using Microsoft.Graph;

namespace TodoExtended.Web.Services;

public class GraphTodoService(GraphServiceClient graphClient) : ITodoService
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
            .Select(t => new TodoTask(
                t.Id!,
                t.Title ?? "Untitled",
                t.Body?.Content,
                t.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                ParseDueDate(t.DueDateTime),
                t.Importance?.ToString()))
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync()
    {
        var lists = await GetTaskListsAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);
        var filter = $"dueDateTime/dateTime ge '{today:yyyy-MM-dd}T00:00:00' and dueDateTime/dateTime lt '{tomorrow:yyyy-MM-dd}T00:00:00'";
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
    /// Extracts a date-only value from Graph's dateTimeTimeZone.
    /// Due dates in To Do are date-only concepts; the time/timezone are discarded
    /// to prevent timezone-induced date shifts.
    /// </summary>
    private static DateOnly? ParseDueDate(Microsoft.Graph.Models.DateTimeTimeZone? dueDateTime)
    {
        if (dueDateTime?.DateTime is null) return null;

        return DateOnly.FromDateTime(
            DateTime.Parse(dueDateTime.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }
}
