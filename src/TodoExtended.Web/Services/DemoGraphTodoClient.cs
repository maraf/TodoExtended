using System.Globalization;

namespace TodoExtended.Web.Services;

/// <summary>
/// Demo implementation of <see cref="IGraphTodoClient"/> that returns in-memory
/// mock data from <see cref="DemoDataStore"/> instead of calling MS Graph.
/// </summary>
public class DemoGraphTodoClient(DemoDataStore store) : IGraphTodoClient
{
    // Opaque sentinel used as a delta link once the initial sync is complete.
    // Any non-empty value causes subsequent delta pages to return no changes.
    private const string DeltaLinkBase = "demo-delta://synced";

    public Task<IReadOnlyList<Microsoft.Graph.Models.TodoTaskList>> GetTaskListsAsync()
    {
        var lists = store.GetTaskLists()
            .Select(l => new Microsoft.Graph.Models.TodoTaskList { Id = l.Id, DisplayName = l.DisplayName })
            .ToList();
        return Task.FromResult<IReadOnlyList<Microsoft.Graph.Models.TodoTaskList>>(lists);
    }

    public Task<IReadOnlyList<Microsoft.Graph.Models.TodoTask>> GetTasksAsync(string taskListId, string? filter = null)
    {
        var tasks = store.GetTasks(taskListId)
            .Select(ToGraphTask)
            .ToList();
        return Task.FromResult<IReadOnlyList<Microsoft.Graph.Models.TodoTask>>(tasks);
    }

    public Task<Microsoft.Graph.Models.TodoTask?> GetTaskAsync(string taskListId, string taskId)
    {
        var task = store.GetTasks(taskListId).FirstOrDefault(t => t.Id == taskId);
        return Task.FromResult(task == null ? null : (Microsoft.Graph.Models.TodoTask?)ToGraphTask(task));
    }

    public Task<Microsoft.Graph.Models.TodoTask> CreateTaskAsync(string taskListId, Microsoft.Graph.Models.TodoTask task)
    {
        var created = store.CreateTask(taskListId, task.Title ?? "Untitled", ParseDueDate(task.DueDateTime))
            ?? throw new InvalidOperationException($"Task list '{taskListId}' not found.");
        return Task.FromResult(ToGraphTask(created));
    }

    public Task PatchTaskAsync(string taskListId, string taskId, Microsoft.Graph.Models.TodoTask patch)
    {
        if (patch.Status.HasValue)
        {
            var completed = patch.Status == Microsoft.Graph.Models.TaskStatus.Completed;
            store.UpdateTaskStatus(taskListId, taskId, completed);
        }
        if (patch.DueDateTime is not null)
        {
            var dueDate = ParseDueDate(patch.DueDateTime);
            if (dueDate.HasValue)
                store.UpdateTaskDueDate(taskListId, taskId, dueDate.Value);
        }
        return Task.CompletedTask;
    }

    public Task<GraphDeltaPage<Microsoft.Graph.Models.TodoTaskList>> GetListsDeltaPageAsync(string? deltaOrNextLink)
    {
        // Initial sync: return all demo lists. Subsequent calls: no changes.
        if (string.IsNullOrEmpty(deltaOrNextLink))
        {
            var lists = store.GetTaskLists()
                .Select(l => new Microsoft.Graph.Models.TodoTaskList { Id = l.Id, DisplayName = l.DisplayName })
                .ToList();
            return Task.FromResult(new GraphDeltaPage<Microsoft.Graph.Models.TodoTaskList>(
                lists, null, $"{DeltaLinkBase}/lists"));
        }

        return Task.FromResult(new GraphDeltaPage<Microsoft.Graph.Models.TodoTaskList>(
            [], null, deltaOrNextLink));
    }

    public Task<GraphDeltaPage<Microsoft.Graph.Models.TodoTask>> GetTasksDeltaPageAsync(string listId, string? deltaOrNextLink)
    {
        // Initial sync: return all tasks for this list. Subsequent calls: no changes.
        if (string.IsNullOrEmpty(deltaOrNextLink))
        {
            var tasks = store.GetTasks(listId)
                .Select(ToGraphTask)
                .ToList();
            return Task.FromResult(new GraphDeltaPage<Microsoft.Graph.Models.TodoTask>(
                tasks, null, $"{DeltaLinkBase}/{listId}"));
        }

        return Task.FromResult(new GraphDeltaPage<Microsoft.Graph.Models.TodoTask>(
            [], null, deltaOrNextLink));
    }

    public async Task<IReadOnlyDictionary<string, GraphDeltaPage<Microsoft.Graph.Models.TodoTask>>> GetTasksDeltaBatchAsync(
        IReadOnlyList<(string ListId, string? DeltaOrNextLink)> requests)
    {
        var results = new Dictionary<string, GraphDeltaPage<Microsoft.Graph.Models.TodoTask>>();
        foreach (var (listId, deltaOrNextLink) in requests)
            results[listId] = await GetTasksDeltaPageAsync(listId, deltaOrNextLink);
        return results;
    }

    private static Microsoft.Graph.Models.TodoTask ToGraphTask(DemoTaskItem t)
    {
        var task = new Microsoft.Graph.Models.TodoTask
        {
            Id = t.Id,
            Title = t.Title,
            Status = t.IsCompleted
                ? Microsoft.Graph.Models.TaskStatus.Completed
                : Microsoft.Graph.Models.TaskStatus.NotStarted,
        };

        if (t.DueDate.HasValue)
        {
            task.DueDateTime = new Microsoft.Graph.Models.DateTimeTimeZone
            {
                DateTime = t.DueDate.Value.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                TimeZone = "UTC",
            };
        }

        if (!string.IsNullOrEmpty(t.Importance) &&
            Enum.TryParse<Microsoft.Graph.Models.Importance>(t.Importance, ignoreCase: true, out var importance))
        {
            task.Importance = importance;
        }

        if (t.HasReminder)
        {
            task.IsReminderOn = true;
            // Use tomorrow 09:00 UTC as a representative reminder time for demo tasks.
            task.ReminderDateTime = new Microsoft.Graph.Models.DateTimeTimeZone
            {
                DateTime = DateTime.UtcNow.Date.AddDays(1).AddHours(9).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                TimeZone = "UTC",
            };
        }

        if (t.IsRecurring)
        {
            task.Recurrence = new Microsoft.Graph.Models.PatternedRecurrence
            {
                Pattern = new Microsoft.Graph.Models.RecurrencePattern
                {
                    Type = Microsoft.Graph.Models.RecurrencePatternType.Daily,
                    Interval = 1,
                },
                Range = new Microsoft.Graph.Models.RecurrenceRange
                {
                    Type = Microsoft.Graph.Models.RecurrenceRangeType.NoEnd,
                },
            };
        }

        return task;
    }

    private static DateOnly? ParseDueDate(Microsoft.Graph.Models.DateTimeTimeZone? dueDateTime)
    {
        if (dueDateTime?.DateTime is null) return null;
        var dt = DateTime.Parse(dueDateTime.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None);
        return DateOnly.FromDateTime(dt);
    }
}
