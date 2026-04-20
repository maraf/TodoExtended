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

    public static IEnumerable<string> GetUsernames(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claimType in new[] { ClaimTypes.Email, "preferred_username", ClaimTypes.Upn })
        {
            foreach (var claim in user.FindAll(claimType))
            {
                if (!string.IsNullOrWhiteSpace(claim.Value) && seen.Add(claim.Value))
                {
                    yield return claim.Value;
                }
            }
        }
    }

    public static AiChatProvider ResolveProvider(AiChatOptions options, ClaimsPrincipal? user)
    {
        var azureConfigured = HasAzureOpenAI(options.AzureOpenAI);
        var usernames = GetUsernames(user);
        var useAzure = azureConfigured
            && usernames.Any(username =>
                options.AzureOpenAIUsers.Any(u => string.Equals(u, username, StringComparison.OrdinalIgnoreCase)));

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
