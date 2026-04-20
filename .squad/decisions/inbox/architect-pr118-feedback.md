# AI Chat Provider Config — Enablement & Fallback Logic

**Date:** 2026-04-20  
**Author:** Architect  
**Status:** Implemented  
**PR:** #118

## Decisions

### 1. Startup enablement must match runtime routing

`hasAzureOpenAI` at startup now requires all three fields (Endpoint + ApiKey + DeploymentName), matching the `azureConfigured` check at runtime. This prevents entering the ChatService branch with incomplete Azure config. All checks use `IsNullOrWhiteSpace` instead of `IsNullOrEmpty`.

### 2. Azure-only deployments: graceful fallback

When only Azure OpenAI is configured (no GitHub Models key), non-allowlisted users now fall back to Azure instead of throwing `InvalidOperationException`. The `AzureOpenAIUsers` list remains a *preferred-routing* mechanism, not a gating one. This enables Azure-only deployments without requiring every user in the allowlist.

### 3. AiChatOptions XML docs match routing logic

`AzureOpenAIUsers` doc now lists all three claim types checked: email, preferred_username, and UPN.

## Validation

- ✅ Build: 0 errors, 0 warnings
- ✅ Tests: 65/65 passing
- ✅ Pushed as d6b836f
