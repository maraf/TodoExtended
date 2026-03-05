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
                t.DueDateTime?.DateTime is not null
                    ? DateTimeOffset.Parse(t.DueDateTime.DateTime)
                    : null,
                t.Importance?.ToString()))
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync()
    {
        var lists = await GetTaskListsAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<TodoTaskWithList>();

        foreach (var list in lists)
        {
            var response = await graphClient.Me.Todo.Lists[list.Id].Tasks.GetAsync();
            if (response?.Value is null) continue;

            foreach (var t in response.Value)
            {
                if (t.DueDateTime?.DateTime is null) continue;

                var due = DateTimeOffset.Parse(t.DueDateTime.DateTime);
                if (DateOnly.FromDateTime(due.Date) != today) continue;

                result.Add(new TodoTaskWithList(
                    t.Id!,
                    t.Title ?? "Untitled",
                    t.Body?.Content,
                    t.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                    due,
                    t.Importance?.ToString(),
                    list.Id,
                    list.DisplayName));
            }
        }

        return result;
    }
}
