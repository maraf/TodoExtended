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
    /// Streams text fragments while retaining the final proposal and task-list metadata.
    /// Providers without streaming support deliver a single text fragment.
    /// </summary>
    async Task<ChatResponse> SendStreamingMessageAsync(
        string userMessage,
        IReadOnlyList<ChatMessage> history,
        Func<string, Task> onText,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await SendMessageAsync(userMessage, history, ct);
        await onText(response.Text);
        return response;
    }

    /// <summary>
    /// Executes the subset of proposed actions that the user approved.
    /// </summary>
    Task<IReadOnlyList<ActionResult>> ExecuteActionsAsync(
        IReadOnlyList<ProposedAction> actions,
        IReadOnlyList<ActionConfirmation> confirmations,
        CancellationToken ct = default);
}
