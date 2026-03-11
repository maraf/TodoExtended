namespace TodoExtended.Web.Services.AiChat;

/// <summary>
/// Configuration for the AI chat feature, bound from the "AiChat" config section.
/// </summary>
public class AiChatOptions
{
    public const string SectionName = "AiChat";

    /// <summary>
    /// OpenAI-compatible endpoint (GitHub Models default).
    /// </summary>
    public string Endpoint { get; set; } = "https://models.github.ai/inference";

    /// <summary>
    /// Model identifier to use for chat completions.
    /// </summary>
    public string Model { get; set; } = "openai/gpt-4.1-mini";

    /// <summary>
    /// API key for the inference endpoint. Store in user-secrets or environment variables.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Maximum number of prior messages included in the conversation context.
    /// </summary>
    public int MaxHistoryMessages { get; set; } = 20;
}
