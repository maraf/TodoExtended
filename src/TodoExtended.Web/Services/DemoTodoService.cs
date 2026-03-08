namespace TodoExtended.Web.Services;

public class DemoTodoService(DemoDataStore store) : ITodoService
{
    public Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync()
    {
        var lists = store.GetTaskLists()
            .Select(l => new TodoTaskList(l.Id, l.DisplayName))
            .ToList();
        return Task.FromResult<IReadOnlyList<TodoTaskList>>(lists);
    }

    public Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId)
    {
        var tasks = store.GetTasks(taskListId)
            .Select(t => new TodoTask(t.Id, t.Title, null, t.IsCompleted, t.DueDate, t.Importance))
            .ToList();
        return Task.FromResult<IReadOnlyList<TodoTask>>(tasks);
    }

    public Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<TodoTaskWithList>();

        foreach (var list in store.GetTaskLists())
        {
            foreach (var task in list.Tasks.Where(t => t.DueDate == today && !t.IsCompleted))
            {
                result.Add(new TodoTaskWithList(
                    task.Id, task.Title, null, task.IsCompleted,
                    task.DueDate, task.Importance,
                    list.Id, list.DisplayName));
            }
        }

        return Task.FromResult<IReadOnlyList<TodoTaskWithList>>(result);
    }

    public Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate, TimeOnly? reminderTime = null)
    {
        var task = store.CreateTask(taskListId, title, dueDate)
            ?? throw new InvalidOperationException($"Task list '{taskListId}' not found.");

        return Task.FromResult(new TodoTask(task.Id, task.Title, null, false, dueDate, null));
    }

    public Task UpdateTaskStatusAsync(string taskListId, string taskId, bool completed)
    {
        store.UpdateTaskStatus(taskListId, taskId, completed);
        return Task.CompletedTask;
    }

    public Task SetTaskListSyncedAsync(string taskListId, bool isSynced) => Task.CompletedTask;

    public Task<IReadOnlyList<TodoTaskList>> GetNotSyncedTaskListsAsync() =>
        Task.FromResult<IReadOnlyList<TodoTaskList>>([]);
}
