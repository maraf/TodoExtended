# Architect History

<!-- Session logs appended by Scribe -->

## Learnings

### 2025-07-19 — Task Templates Design
- Designed the Task Templates feature: local SQLite storage via EF Core, `ITemplateService` for CRUD + execution, `CreateTaskAsync` added to `ITodoService`.
- Key pattern: templates store `TaskListId` + cached `TaskListName` to avoid Graph calls for display. Accepted staleness tradeoff.
- Home page gets quick-create buttons; new `/templates` page for CRUD management.
- Project uses .NET 10, Blazor InteractiveServer, primary constructors, file-scoped namespaces, `[PersistentState]`, and consistent MSAL exception handling across pages.
- No user ID needed — single-user local app. Noted as a future concern if multi-user is ever needed.
- Auto-migrate at startup is appropriate for a local-only SQLite app.

### 2026-03-06 — API Key Authentication & Token Persistence Design
- Designed complete API key authentication system for TodoExtended: per-user API keys, stored MSAL token caches, and three REST endpoints.
- **Key insight:** Leverage MSAL's token cache infrastructure via custom `ITokenCacheSerializer` backed by SQLite + ASP.NET Data Protection for encryption. Avoids manual token parsing/refresh logic.
- **Multi-user pivot:** Introduced `User` entity (Entra ID OID as PK), `ApiKey` (SHA256 hashed), and `UserToken` (encrypted MSAL cache). Single-user assumption now lifted.
- **Dual authentication:** Custom `ApiKeyAuthenticationHandler` coexists with OIDC. Both use same `GraphServiceClient` via custom `DelegatingAuthenticationProvider` that switches between MSAL flows based on auth type.
- **Token capture:** `UserRegistrationMiddleware` runs after auth to ensure every OIDC sign-in creates/updates `User` record. MSAL cache persistence hooks into token cache events.
- **Security:** API keys hashed with SHA256; token cache encrypted with Data Protection; keys shown once on creation, never retrievable again.
- **Endpoint design:** Minimal APIs for `/api/templates`, `/api/templates/{id}/execute`, `/api/today`, plus CRUD endpoints for API key management.
- **Migration path:** Additive changes only. Existing Blazor pages unaffected. On next OIDC sign-in, users auto-register and can create API keys.
- **Open questions:** Key expiration, per-key scopes, audit logging, CORS policy for browser clients.
