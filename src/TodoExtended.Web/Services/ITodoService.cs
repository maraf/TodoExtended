namespace TodoExtended.Web.Services;

public record TodoTaskList(string Id, string DisplayName);

public record TodoTask(
    string Id,
    string Title,
    string? Body,
    bool IsCompleted,
    DateTimeOffset? DueDateTime,
    string? Importance);

public interface ITodoService
{
    Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync();
    Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId);
}
