namespace TodoExtended.Web.Services;

public record TodoTaskList(string Id, string DisplayName, bool IsArchived = false);

public record TodoTask(
    string Id,
    string Title,
    string? Body,
    bool IsCompleted,
    DateOnly? DueDate,
    string? Importance);

public record TodoTaskWithList(
    string Id,
    string Title,
    string? Body,
    bool IsCompleted,
    DateOnly? DueDate,
    string? Importance,
    string ListId,
    string ListName);

public interface ITodoService
{
    Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync();
    Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId);
    Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync();
    Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate);
    Task UpdateTaskStatusAsync(string taskListId, string taskId, bool completed);
    Task SetTaskListArchivedAsync(string taskListId, bool isArchived);
    Task<IReadOnlyList<TodoTaskList>> GetArchivedTaskListsAsync();
}
