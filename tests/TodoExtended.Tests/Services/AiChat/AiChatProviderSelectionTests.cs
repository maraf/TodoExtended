using System.Security.Claims;
using TodoExtended.Web.Services.AiChat;

namespace TodoExtended.Tests.Services.AiChat;

public class AiChatProviderSelectionTests
{
    [Fact]
    public void HasAzureOpenAI_WhitespaceDeploymentName_ReturnsFalse()
    {
        var options = CreateOptions();
        options.AzureOpenAI.ApiKey = "azure-key";
        options.AzureOpenAI.Endpoint = "https://example.openai.azure.com/";
        options.AzureOpenAI.DeploymentName = " ";

        var configured = AiChatProviderSelection.HasAzureOpenAI(options.AzureOpenAI);

        Assert.False(configured);
    }

    [Fact]
    public void ResolveProvider_IncompleteAzureConfigurationWithoutGitHubModels_ReturnsNone()
    {
        var options = CreateOptions();
        options.AzureOpenAI.ApiKey = "azure-key";
        options.AzureOpenAI.Endpoint = "https://example.openai.azure.com/";

        var provider = AiChatProviderSelection.ResolveProvider(options, CreateUser(ClaimTypes.Email, "user@example.com"));

        Assert.Equal(AiChatProvider.None, provider);
    }

    [Fact]
    public void ResolveProvider_AzureOnlyConfigurationWithNonAllowlistedUser_ReturnsAzureOpenAI()
    {
        var options = CreateOptions();
        options.AzureOpenAI.ApiKey = "azure-key";
        options.AzureOpenAI.Endpoint = "https://example.openai.azure.com/";
        options.AzureOpenAI.DeploymentName = "gpt-4o";
        options.AzureOpenAIUsers.Add("allowlisted@example.com");

        var provider = AiChatProviderSelection.ResolveProvider(options, CreateUser(ClaimTypes.Email, "other@example.com"));

        Assert.Equal(AiChatProvider.AzureOpenAI, provider);
    }

    [Fact]
    public void ResolveProvider_AzureOnlyConfigurationWithoutUsernameClaim_ReturnsAzureOpenAI()
    {
        var options = CreateOptions();
        options.AzureOpenAI.ApiKey = "azure-key";
        options.AzureOpenAI.Endpoint = "https://example.openai.azure.com/";
        options.AzureOpenAI.DeploymentName = "gpt-4o";

        var provider = AiChatProviderSelection.ResolveProvider(options, new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Equal(AiChatProvider.AzureOpenAI, provider);
    }

    [Fact]
    public void ResolveProvider_AllowlistedIdentityClaims_ReturnsAzureOpenAI()
    {
        var options = CreateOptions();
        options.GitHubModels.ApiKey = "github-key";
        options.AzureOpenAI.ApiKey = "azure-key";
        options.AzureOpenAI.Endpoint = "https://example.openai.azure.com/";
        options.AzureOpenAI.DeploymentName = "gpt-4o";
        options.AzureOpenAIUsers.Add("ALLOWLISTED@EXAMPLE.COM");

        var upnProvider = AiChatProviderSelection.ResolveProvider(options, CreateUser(ClaimTypes.Upn, "allowlisted@example.com"));
        var preferredUsernameProvider = AiChatProviderSelection.ResolveProvider(options, CreateUser("preferred_username", "allowlisted@example.com"));

        Assert.Equal(AiChatProvider.AzureOpenAI, upnProvider);
        Assert.Equal(AiChatProvider.AzureOpenAI, preferredUsernameProvider);
    }

    [Fact]
    public void ResolveProvider_BothProvidersConfiguredWithNonAllowlistedUser_ReturnsGitHubModels()
    {
        var options = CreateOptions();
        options.GitHubModels.ApiKey = "github-key";
        options.AzureOpenAI.ApiKey = "azure-key";
        options.AzureOpenAI.Endpoint = "https://example.openai.azure.com/";
        options.AzureOpenAI.DeploymentName = "gpt-4o";
        options.AzureOpenAIUsers.Add("allowlisted@example.com");

        var provider = AiChatProviderSelection.ResolveProvider(options, CreateUser(ClaimTypes.Email, "other@example.com"));

        Assert.Equal(AiChatProvider.GitHubModels, provider);
    }

    private static AiChatOptions CreateOptions() => new();

    private static ClaimsPrincipal CreateUser(string claimType, string claimValue) =>
        new(new ClaimsIdentity([new Claim(claimType, claimValue)], "test"));
}
