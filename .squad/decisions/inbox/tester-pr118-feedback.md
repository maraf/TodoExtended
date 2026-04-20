# Tester coverage note — PR 118 feedback

## Scope

Regression coverage for AI provider selection lives in `tests\TodoExtended.Tests\Services\AiChat\AiChatProviderSelectionTests.cs` and validates the accepted PR 118 feedback against `AiChatProviderSelection`.

## Covered scenarios

1. **Incomplete Azure configuration is ignored**
   - `HasAzureOpenAI_WhitespaceDeploymentName_ReturnsFalse`
   - `ResolveProvider_IncompleteAzureConfigurationWithoutGitHubModels_ReturnsNone`
2. **Azure-only deployments still work without allowlist matches**
   - `ResolveProvider_AzureOnlyConfigurationWithNonAllowlistedUser_ReturnsAzureOpenAI`
   - `ResolveProvider_AzureOnlyConfigurationWithoutUsernameClaim_ReturnsAzureOpenAI`
3. **Allowlisted identities route to Azure**
   - `ResolveProvider_AllowlistedIdentityClaims_ReturnsAzureOpenAI`
4. **Dual-provider fallback remains GitHub Models for non-allowlisted users**
   - `ResolveProvider_BothProvidersConfiguredWithNonAllowlistedUser_ReturnsGitHubModels`

## Validation

- Ran `dotnet test tests\TodoExtended.Tests\TodoExtended.Tests.csproj --filter "FullyQualifiedName~TodoExtended.Tests.Services.AiChat"`
- Result: **39/39 AI chat tests passing**

## QA conclusion

The provider-selection helper now has direct regression coverage for the accepted PR 118 feedback paths: incomplete Azure config, Azure-only fallback behavior, UPN/preferred_username allowlist routing, and GitHub Models fallback when both providers are configured.
