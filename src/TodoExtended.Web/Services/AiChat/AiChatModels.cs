namespace TodoExtended.Web.Services.AiChat;

/// <summary>
/// Type of action the AI proposes to take on a task.
/// </summary>
public enum TaskActionType
{
    CreateTask,
    CompleteTask,
    UncompleteTask,
    CreateTemplate,
    UpdateTemplate,
    DeleteTemplate,
    ExecuteTemplate
}

/// <summary>
/// An action proposed by the AI, derived from a tool-call response.
/// </summary>
public record ProposedAction(
    TaskActionType Type,
    string Description,
    Dictionary<string, string> Parameters);

/// <summary>
/// User's approval or rejection of a single proposed action.
/// </summary>
public record ActionConfirmation(int ActionIndex, bool Approved);

/// <summary>
/// Result of executing one approved action.
/// </summary>
public record ActionResult(int ActionIndex, bool Success, string Message);

/// <summary>
/// A single message in the chat conversation history.
/// </summary>
public record ChatMessage(
    string Role,
    string? Text,
    IReadOnlyList<ProposedAction>? ProposedActions,
    DateTimeOffset Timestamp);

/// <summary>
/// Response from the AI containing text and any proposed actions.
/// </summary>
public record ChatResponse(
    string Text,
    IReadOnlyList<ProposedAction> ProposedActions);
