# Backend PR 118 Feedback

- Accepted the Azure configuration feedback: Azure is enabled only when `Endpoint`, `ApiKey`, and `DeploymentName` are all present and non-whitespace.
- Chose Azure OpenAI as the safe fallback provider when GitHub Models is not configured but Azure is fully configured. This keeps chat available while preserving the existing preference order where GitHub Models remains the default whenever it exists.
- Updated the allowlist documentation to mention UPN because runtime provider routing checks `email`, `preferred_username`, and `ClaimTypes.Upn`.
