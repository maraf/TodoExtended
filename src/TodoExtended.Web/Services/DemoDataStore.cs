using System.Text.Json;

namespace TodoExtended.Web.Services;

public class DemoDataStore
{
    private readonly List<DemoTaskList> _taskLists;

    public DemoDataStore()
    {
        var assembly = typeof(DemoDataStore).Assembly;
        using var stream = assembly.GetManifestResourceStream("TodoExtended.Web.DemoData.demo-tasks.json")
            ?? throw new InvalidOperationException("Demo data resource 'TodoExtended.Web.DemoData.demo-tasks.json' not found.");

        var data = JsonSerializer.Deserialize<DemoData>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize demo data.");

        _taskLists = data.TaskLists;

        // Set a couple of tasks as due today so the Today view shows items
        var today = DateOnly.FromDateTime(DateTime.Today);
        SetDueDate("demo-list-work", "demo-task-w1", today);
        SetDueDate("demo-list-personal", "demo-task-p1", today);
    }

    public IReadOnlyList<DemoTaskList> GetTaskLists() => _taskLists.AsReadOnly();

    public IReadOnlyList<DemoTaskItem> GetTasks(string listId)
    {
        var list = _taskLists.FirstOrDefault(l => l.Id == listId);
        return list?.Tasks.AsReadOnly() ?? (IReadOnlyList<DemoTaskItem>)[];
    }

    public DemoTaskItem? CreateTask(string listId, string title, DateOnly? dueDate)
    {
        var list = _taskLists.FirstOrDefault(l => l.Id == listId);
        if (list is null) return null;

        var task = new DemoTaskItem
        {
            Id = $"demo-task-{Guid.NewGuid():N}",
            Title = title,
            IsCompleted = false,
            DueDate = dueDate
        };
        list.Tasks.Add(task);
        return task;
    }

    public bool UpdateTaskStatus(string listId, string taskId, bool completed)
    {
        var list = _taskLists.FirstOrDefault(l => l.Id == listId);
        if (list is null) return false;

        var task = list.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return false;

        task.IsCompleted = completed;
        return true;
    }

    private void SetDueDate(string listId, string taskId, DateOnly date)
    {
        var list = _taskLists.FirstOrDefault(l => l.Id == listId);
        var task = list?.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is not null)
            task.DueDate = date;
    }
}

public class DemoData
{
    public List<DemoTaskList> TaskLists { get; set; } = [];
}

public class DemoTaskList
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<DemoTaskItem> Tasks { get; set; } = [];
}

public class DemoTaskItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Importance { get; set; }
}

