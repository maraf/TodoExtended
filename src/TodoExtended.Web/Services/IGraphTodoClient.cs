namespace TodoExtended.Web.Services;

public record GraphDeltaPage<T>(
    IReadOnlyList<T> Value,
    string? OdataNextLink,
    string? OdataDeltaLink);

public interface IGraphTodoClient
{
    Task<IReadOnlyList<Microsoft.Graph.Models.TodoTaskList>> GetTaskListsAsync();
    Task<IReadOnlyList<Microsoft.Graph.Models.TodoTask>> GetTasksAsync(string taskListId, string? filter = null);
    Task<Microsoft.Graph.Models.TodoTask> CreateTaskAsync(string taskListId, Microsoft.Graph.Models.TodoTask task);
    Task PatchTaskAsync(string taskListId, string taskId, Microsoft.Graph.Models.TodoTask patch);
    Task<GraphDeltaPage<Microsoft.Graph.Models.TodoTaskList>> GetListsDeltaPageAsync(string? deltaOrNextLink);
    Task<GraphDeltaPage<Microsoft.Graph.Models.TodoTask>> GetTasksDeltaPageAsync(string listId, string? deltaOrNextLink);
}
