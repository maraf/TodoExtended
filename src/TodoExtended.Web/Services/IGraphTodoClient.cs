namespace TodoExtended.Web.Services;

public record GraphDeltaPage<T>(
    IReadOnlyList<T> Value,
    string? OdataNextLink,
    string? OdataDeltaLink);

public interface IGraphTodoClient
{
    Task<IReadOnlyList<Microsoft.Graph.Models.TodoTaskList>> GetTaskListsAsync();
    Task<IReadOnlyList<Microsoft.Graph.Models.TodoTask>> GetTasksAsync(string taskListId, string? filter = null);
    Task<Microsoft.Graph.Models.TodoTask?> GetTaskAsync(string taskListId, string taskId);
    Task<Microsoft.Graph.Models.TodoTask> CreateTaskAsync(string taskListId, Microsoft.Graph.Models.TodoTask task);
    Task PatchTaskAsync(string taskListId, string taskId, Microsoft.Graph.Models.TodoTask patch);
    Task<GraphDeltaPage<Microsoft.Graph.Models.TodoTaskList>> GetListsDeltaPageAsync(string? deltaOrNextLink);
    Task<GraphDeltaPage<Microsoft.Graph.Models.TodoTask>> GetTasksDeltaPageAsync(string listId, string? deltaOrNextLink);

    /// <summary>
    /// Batch-fetches the first delta page for multiple lists in a single HTTP call (max 20 per batch).
    /// Returns a dictionary mapping each list ID to its delta page result.
    /// </summary>
    Task<IReadOnlyDictionary<string, GraphDeltaPage<Microsoft.Graph.Models.TodoTask>>> GetTasksDeltaBatchAsync(
        IReadOnlyList<(string ListId, string? DeltaOrNextLink)> requests);

    Task<Microsoft.Graph.Models.Subscription> CreateSubscriptionAsync(
        string resource,
        string notificationUrl,
        string clientState,
        DateTimeOffset expirationDateTime,
        CancellationToken cancellationToken = default);

    Task<Microsoft.Graph.Models.Subscription?> RenewSubscriptionAsync(
        string subscriptionId,
        DateTimeOffset expirationDateTime,
        CancellationToken cancellationToken = default);

    Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
}
