namespace TodoExtended.Web.Services.AiChat;

/// <summary>
/// Configuration for the AI chat feature, bound from the "AiChat" config section.
/// Supports two providers: GitHub Models (default) and Azure OpenAI (production).
/// </summary>
public class AiChatOptions
{
    public const string SectionName = "AiChat";

    /// <summary>
    /// Maximum number of prior messages included in the conversation context.
    /// </summary>
    public int MaxHistoryMessages { get; set; } = 20;

    /// <summary>
    /// GitHub Models provider configuration (used by default).
    /// </summary>
    public GitHubModelsOptions GitHubModels { get; set; } = new();

    /// <summary>
    /// Azure OpenAI provider configuration (used for production-grade workloads).
    /// </summary>
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();

    /// <summary>
    /// List of usernames (email / preferred_username / UPN) whose chat requests should be
    /// routed to Azure OpenAI instead of GitHub Models. Matching is case-insensitive.
    /// </summary>
    public List<string> AzureOpenAIUsers { get; set; } = new();
}

/// <summary>
/// Configuration for the GitHub Models (OpenAI-compatible) provider.
/// </summary>
public class GitHubModelsOptions
{
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
}

/// <summary>
/// Configuration for the Azure OpenAI provider.
/// </summary>
public class AzureOpenAIOptions
{
    /// <summary>
    /// Azure OpenAI resource endpoint (e.g. "https://my-resource.openai.azure.com/").
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Name of the deployed model in the Azure OpenAI resource.
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// API key for the Azure OpenAI resource. Store in user-secrets or environment variables.
    /// </summary>
    public string? ApiKey { get; set; }
}
