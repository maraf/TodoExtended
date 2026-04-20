using System.Security.Claims;

namespace TodoExtended.Web.Services.AiChat;

public enum AiChatProvider
{
    None = 0,
    GitHubModels = 1,
    AzureOpenAI = 2
}

public static class AiChatProviderSelection
{
    public static bool HasGitHubModels(string? apiKey) =>
        !string.IsNullOrWhiteSpace(apiKey);

    public static bool HasAzureOpenAI(string? apiKey, string? endpoint, string? deploymentName) =>
        !string.IsNullOrWhiteSpace(apiKey)
        && !string.IsNullOrWhiteSpace(endpoint)
        && !string.IsNullOrWhiteSpace(deploymentName);

    public static bool HasAzureOpenAI(AzureOpenAIOptions options) =>
        HasAzureOpenAI(options.ApiKey, options.Endpoint, options.DeploymentName);

    public static string? GetUsername(ClaimsPrincipal? user) =>
        user?.FindFirst(ClaimTypes.Email)?.Value
        ?? user?.FindFirst("preferred_username")?.Value
        ?? user?.FindFirst(ClaimTypes.Upn)?.Value;

    public static AiChatProvider ResolveProvider(AiChatOptions options, ClaimsPrincipal? user)
    {
        var azureConfigured = HasAzureOpenAI(options.AzureOpenAI);
        var username = GetUsername(user);
        var useAzure = azureConfigured
            && !string.IsNullOrWhiteSpace(username)
            && options.AzureOpenAIUsers.Any(u => string.Equals(u, username, StringComparison.OrdinalIgnoreCase));

        if (useAzure)
        {
            return AiChatProvider.AzureOpenAI;
        }

        if (HasGitHubModels(options.GitHubModels.ApiKey))
        {
            return AiChatProvider.GitHubModels;
        }

        if (azureConfigured)
        {
            return AiChatProvider.AzureOpenAI;
        }

        return AiChatProvider.None;
    }
}
