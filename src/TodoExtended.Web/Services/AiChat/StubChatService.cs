namespace TodoExtended.Web.Services.AiChat;

/// <summary>
/// Placeholder implementation returned when no AI backend is configured.
/// Used in demo mode and as a fallback until the real ChatService is wired up.
/// </summary>
public class StubChatService : IChatService
{
    public Task<ChatResponse> SendMessageAsync(
        string userMessage,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        var response = new ChatResponse(
            Text: "AI chat is not configured. Set AiChat:GitHubModels:ApiKey (or configure AiChat:AzureOpenAI) to enable this feature.",
            ProposedActions: []);

        return Task.FromResult(response);
    }

    public Task<IReadOnlyList<ActionResult>> ExecuteActionsAsync(
        IReadOnlyList<ProposedAction> actions,
        IReadOnlyList<ActionConfirmation> confirmations,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ActionResult>>([]);
    }
}
