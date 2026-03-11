namespace TodoExtended.Web.Services.AiChat;

/// <summary>
/// Sends user messages to the AI and executes approved actions.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Sends a user message along with conversation history to the AI.
    /// Returns text and any proposed task actions derived from tool calls.
    /// </summary>
    Task<ChatResponse> SendMessageAsync(
        string userMessage,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default);

    /// <summary>
    /// Executes the subset of proposed actions that the user approved.
    /// </summary>
    Task<IReadOnlyList<ActionResult>> ExecuteActionsAsync(
        IReadOnlyList<ProposedAction> actions,
        IReadOnlyList<ActionConfirmation> confirmations,
        CancellationToken ct = default);
}
