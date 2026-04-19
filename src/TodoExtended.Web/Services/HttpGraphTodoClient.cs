using Microsoft.Graph;
using TasksDeltaResponse = Microsoft.Graph.Me.Todo.Lists.Item.Tasks.Delta.DeltaGetResponse;

namespace TodoExtended.Web.Services;

public class HttpGraphTodoClient(GraphServiceClient graphClient) : IGraphTodoClient
{
    public async Task<IReadOnlyList<Microsoft.Graph.Models.TodoTaskList>> GetTaskListsAsync()
    {
        var response = await graphClient.Me.Todo.Lists.GetAsync();
        return response?.Value?.AsReadOnly() ?? (IReadOnlyList<Microsoft.Graph.Models.TodoTaskList>)[];
    }

    public async Task<IReadOnlyList<Microsoft.Graph.Models.TodoTask>> GetTasksAsync(string taskListId, string? filter = null)
    {
        var response = await graphClient.Me.Todo.Lists[taskListId].Tasks.GetAsync(config =>
        {
            if (!string.IsNullOrEmpty(filter))
                config.QueryParameters.Filter = filter;
        });
        return response?.Value?.AsReadOnly() ?? (IReadOnlyList<Microsoft.Graph.Models.TodoTask>)[];
    }

    public async Task<Microsoft.Graph.Models.TodoTask?> GetTaskAsync(string taskListId, string taskId)
    {
        return await graphClient.Me.Todo.Lists[taskListId].Tasks[taskId].GetAsync();
    }

    public async Task<Microsoft.Graph.Models.TodoTask> CreateTaskAsync(string taskListId, Microsoft.Graph.Models.TodoTask task)
    {
        return await graphClient.Me.Todo.Lists[taskListId].Tasks.PostAsync(task)
            ?? throw new InvalidOperationException("Graph API returned null when creating task.");
    }

    public async Task PatchTaskAsync(string taskListId, string taskId, Microsoft.Graph.Models.TodoTask patch)
    {
        await graphClient.Me.Todo.Lists[taskListId].Tasks[taskId].PatchAsync(patch);
    }

    public async Task<GraphDeltaPage<Microsoft.Graph.Models.TodoTaskList>> GetListsDeltaPageAsync(string? deltaOrNextLink)
    {
        var response = string.IsNullOrEmpty(deltaOrNextLink)
            ? await graphClient.Me.Todo.Lists.Delta.GetAsDeltaGetResponseAsync()
            : await graphClient.Me.Todo.Lists.Delta.WithUrl(deltaOrNextLink).GetAsDeltaGetResponseAsync();

        return new GraphDeltaPage<Microsoft.Graph.Models.TodoTaskList>(
            response?.Value?.AsReadOnly() ?? (IReadOnlyList<Microsoft.Graph.Models.TodoTaskList>)[],
            response?.OdataNextLink,
            response?.OdataDeltaLink);
    }

    public async Task<GraphDeltaPage<Microsoft.Graph.Models.TodoTask>> GetTasksDeltaPageAsync(string listId, string? deltaOrNextLink)
    {
        var response = string.IsNullOrEmpty(deltaOrNextLink)
            ? await graphClient.Me.Todo.Lists[listId].Tasks.Delta.GetAsDeltaGetResponseAsync()
            : await graphClient.Me.Todo.Lists[listId].Tasks.Delta.WithUrl(deltaOrNextLink).GetAsDeltaGetResponseAsync();

        return new GraphDeltaPage<Microsoft.Graph.Models.TodoTask>(
            response?.Value?.AsReadOnly() ?? (IReadOnlyList<Microsoft.Graph.Models.TodoTask>)[],
            response?.OdataNextLink,
            response?.OdataDeltaLink);
    }

    public async Task<IReadOnlyDictionary<string, GraphDeltaPage<Microsoft.Graph.Models.TodoTask>>> GetTasksDeltaBatchAsync(
        IReadOnlyList<(string ListId, string? DeltaOrNextLink)> requests)
    {
        if (requests.Count == 0)
            return new Dictionary<string, GraphDeltaPage<Microsoft.Graph.Models.TodoTask>>();

        var results = new Dictionary<string, GraphDeltaPage<Microsoft.Graph.Models.TodoTask>>();

        foreach (var chunk in requests.Chunk(20))
        {
            var batchContent = new BatchRequestContentCollection(graphClient);
            var requestIdToListId = new Dictionary<string, string>();

            foreach (var (listId, deltaOrNextLink) in chunk)
            {
                var deltaBuilder = graphClient.Me.Todo.Lists[listId].Tasks.Delta;
                var requestInfo = string.IsNullOrEmpty(deltaOrNextLink)
                    ? deltaBuilder.ToGetRequestInformation()
                    : deltaBuilder.WithUrl(deltaOrNextLink).ToGetRequestInformation();

                var requestId = await batchContent.AddBatchRequestStepAsync(requestInfo);
                requestIdToListId[requestId] = listId;
            }

            var batchResponse = await graphClient.Batch.PostAsync(batchContent);

            foreach (var (requestId, listId) in requestIdToListId)
            {
                try
                {
                    var response = await batchResponse.GetResponseByIdAsync<TasksDeltaResponse>(requestId);
                    results[listId] = new GraphDeltaPage<Microsoft.Graph.Models.TodoTask>(
                        response?.Value?.AsReadOnly() ?? (IReadOnlyList<Microsoft.Graph.Models.TodoTask>)[],
                        response?.OdataNextLink,
                        response?.OdataDeltaLink);
                }
                catch (Exception)
                {
                    // Individual sub-request failed — return empty page so sync can retry next time
                    results[listId] = new GraphDeltaPage<Microsoft.Graph.Models.TodoTask>([], null, null);
                }
            }
        }

        return results;
    }

    public async Task<Microsoft.Graph.Models.Subscription> CreateSubscriptionAsync(
        string resource,
        string notificationUrl,
        string clientState,
        DateTimeOffset expirationDateTime,
        CancellationToken cancellationToken = default)
    {
        return await graphClient.Subscriptions.PostAsync(new Microsoft.Graph.Models.Subscription
        {
            ChangeType = "created,updated,deleted",
            Resource = resource,
            NotificationUrl = notificationUrl,
            ClientState = clientState,
            ExpirationDateTime = expirationDateTime,
        }, cancellationToken: cancellationToken)
        ?? throw new InvalidOperationException($"Graph returned null when creating a subscription for resource '{resource}'.");
    }

    public Task<Microsoft.Graph.Models.Subscription?> RenewSubscriptionAsync(
        string subscriptionId,
        DateTimeOffset expirationDateTime,
        CancellationToken cancellationToken = default) =>
        graphClient.Subscriptions[subscriptionId].PatchAsync(
            new Microsoft.Graph.Models.Subscription
            {
                ExpirationDateTime = expirationDateTime,
            },
            cancellationToken: cancellationToken);

    public Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default) =>
        graphClient.Subscriptions[subscriptionId].DeleteAsync(cancellationToken: cancellationToken);
}
