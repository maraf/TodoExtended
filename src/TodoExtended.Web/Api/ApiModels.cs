namespace TodoExtended.Web.Api;

public record ApiTodoTask(
    string Id,
    string Title,
    bool IsCompleted,
    DateOnly? DueDate,
    string? Importance);

public record ApiTodoTaskWithList(
    string Id,
    string Title,
    bool IsCompleted,
    DateOnly? DueDate,
    string? Importance,
    string ListId,
    string ListName);
