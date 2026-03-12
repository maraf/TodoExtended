# Architect History

<!-- Session logs appended by Scribe -->

## Recent Work

### 2026-03-12 — Per-User Data Scoping Audit

- **Audited all 8 EF Core entities** for user-scoping. Found `ApiKey`, `UserToken` properly scoped; `TaskTemplate`, `CachedTaskList`, `CachedTask`, `SyncMetadata` are NOT user-scoped.
- **Critical gap: TaskTemplate** has no `UserId` column. `ITemplateService` methods accept no userId parameter. All CRUD and execution is globally shared. Affects `Templates.razor`, `Home.razor`, `GET/POST /api/templates`, and `ChatService`.
- **Critical gap: CachedTodoService** stores cached tasks/lists without UserId. All cache queries return all users' data. `ClearCacheAndInitialSyncAsync` wipes the entire cache for all users.
- **Delta token issue:** `TaskListsDeltaTokenKey = "TaskListsDeltaToken"` is a single global key in SyncMetadata. All users overwrite each other's delta token. Per-list tokens in `CachedTaskList.DeltaToken` are per-list but not per-user.
- **Pattern: ApiKeys.razor** is the model for proper user-scoping — extracts OID from `AuthenticationStateProvider` claims and passes it explicitly to every `IApiKeyService` call. Other pages should follow this pattern.
- **No global query filters** exist in `AppDbContext.OnModelCreating`. Decided against adding them (complexity of injecting user context into DbContext). Prefer explicit service-layer filtering.
- **Design stored:** `.squad/decisions/decisions.md` — full entity audit, gap analysis, proposed schema changes, migration strategy, and phased implementation plan.
- **Key file paths:** `Data/AppDbContext.cs` (8 DbSets, no query filters), `Services/TemplateService.cs` (no userId), `Services/CachedTodoService.cs` (global delta token at line 18, global cache clear at lines 702-711), `Authentication/ApiKeyAuthenticationHandler.cs` (proper claims), `Middleware/UserSyncMiddleware.cs` (proper user creation).

### 2026-03-12 — Per-User Data Scoping Implementation Complete

- **Audit followed by Backend implementation.** All critical gaps now remediated.
- **Schema:** UserId added to TaskTemplate, CachedTaskList, CachedTask. SyncMetadata made per-user via key convention.
- **Service layer:** ITemplateService accepts userId on all CRUD methods. CachedTodoService uses IHttpContextAccessor to extract userId; all cache queries filter by user.
- **Delta tokens:** Per-user via `$"TaskListsDeltaToken:{userId}"` key convention.
- **Sync locks:** Per-user via `ConcurrentDictionary<string, SemaphoreSlim>`.
- **API endpoints:** Extract userId from claims, pass to services.
- **Blazor pages:** Extract userId from AuthenticationStateProvider, pass to services.
- **Build:** Clean (0 errors, 0 warnings), 21 unit tests passing.
- **Backward compat:** EF Core migration assigns orphaned data to first user; demo mode assigns to "demo-user".
- **Design stored:** `.squad/decisions/decisions.md` merged from inbox.
- **Orchestration logged:** `.squad/orchestration-log/20260312T100300Z-architect.md`

## Core Context

**Established Patterns & Key Decisions (pre-2026-03-12):**

1. **Authentication:** Microsoft.Identity.Web (OIDC) + custom API key scheme + SQLite token cache + UserRegistrationMiddleware
2. **UI Framework:** Migrated Flowbite → MudBlazor v9 (Material Design). All components rewritten cleanly.
3. **Garmin Companion:** Device App (Monkey C) with REST API authentication via API key. Phone acts as HTTP proxy. v1 features: view/complete tasks, execute templates.
4. **Task Templates:** Local SQLite storage via ITemplateService, CRUD + execution. Home page quick-create buttons.
5. **Delta Caching:** GraphTodoService decorated by CachedTodoService with SQLite cache + Microsoft Graph delta sync. Handles pagination + soft deletes.
6. **DbContext Lifetime:** Use IDbContextFactory<AppDbContext> for short-lived contexts in Blazor Server (prevents ObjectDisposedException).
7. **API Key Auth:** SHA256 hashing + Data Protection encryption. One-time display on creation.
8. **AI Chat Foundation:** Microsoft.Extensions.AI abstraction. Manual tool loop with read/write tool pattern. Scoped ChatService, singleton IChatClient.
9. **Code Style:** .NET 10, primary constructors, file-scoped namespaces, [PersistentState], consistent MSAL exception handling.



**Status:** Complete  
**Branch:** squad/22-ai-chat

Created shared interfaces, models, and DI scaffold for AI chat feature:
- `IChatService` interface with SendMessageAsync/ExecuteActionsAsync
- `AiChatModels.cs`: ChatMessage, ProposedAction, ActionResult, AiChatOptions
- `StubChatService` placeholder
- Microsoft.Extensions.AI NuGet packages added
- DI scaffold in Program.cs (Development/Demo/Production conditional registration)

**Decisions:**
- SDK: Microsoft.Extensions.AI (provider-agnostic)
- Pattern: Structured tool-calling with manual loop
- Config: AiChatOptions bound to "AiChat" section

** Clean (no errors/warnings)Build:** 

**Orchestration Log:** .squad/orchestration-log/20260311T095047Z-architect.md

### 2026-03-12 — Per-User Data Scoping Audit

- **Audited all 8 EF Core entities** for user-scoping. Found `ApiKey`, `UserToken` properly scoped; `TaskTemplate`, `CachedTaskList`, `CachedTask`, `SyncMetadata` are NOT user-scoped.
- **Critical gap: TaskTemplate** has no `UserId` column. `ITemplateService` methods accept no userId parameter. All CRUD and execution is globally shared. Affects `Templates.razor`, `Home.razor`, `GET/POST /api/templates`, and `ChatService`.
- **Critical gap: CachedTodoService** stores cached tasks/lists without UserId. All cache queries return all users' data. `ClearCacheAndInitialSyncAsync` wipes the entire cache for all users.
- **Delta token issue:** `TaskListsDeltaTokenKey = "TaskListsDeltaToken"` is a single global key in SyncMetadata. All users overwrite each other's delta token. Per-list tokens in `CachedTaskList.DeltaToken` are per-list but not per-user.
- **Pattern: ApiKeys.razor** is the model for proper user-scoping — extracts OID from `AuthenticationStateProvider` claims and passes it explicitly to every `IApiKeyService` call. Other pages should follow this pattern.
- **No global query filters** exist in `AppDbContext.OnModelCreating`. Decided against adding them (complexity of injecting user context into DbContext). Prefer explicit service-layer filtering.
- **Design output:** `.squad/decisions/decisions.md` — full entity audit, gap analysis, proposed schema changes, migration strategy, and phased implementation plan.
- **Key file paths:** `Data/AppDbContext.cs` (8 DbSets, no query filters), `Services/TemplateService.cs` (no userId), `Services/CachedTodoService.cs` (global delta token at line 18, global cache clear at lines 702-711), `Authentication/ApiKeyAuthenticationHandler.cs` (proper claims), `Middleware/UserSyncMiddleware.cs` (proper user creation).

### 2026-03-12 — Per-User Data Scoping Implementation Complete

- **Audit followed by Backend implementation.** All critical gaps now remediated.
- **Schema:** UserId added to TaskTemplate, CachedTaskList, CachedTask. SyncMetadata made per-user via key convention.
- **Service layer:** ITemplateService accepts userId on all CRUD methods. CachedTodoService uses IHttpContextAccessor to extract userId; all cache queries filter by user.
- **Delta tokens:** Per-user via `$"TaskListsDeltaToken:{userId}"` key convention.
- **Sync locks:** Per-user via `ConcurrentDictionary<string, SemaphoreSlim>`.
- **API endpoints:** Extract userId from claims, pass to services.
- **Blazor pages:** Extract userId from AuthenticationStateProvider, pass to services.
- **Build:** Clean (0 errors, 0 warnings), 21 unit tests passing.
- **Backward compat:** EF Core migration assigns orphaned data to first user; demo mode assigns to "demo-user".
- **Design stored:** `.squad/decisions/decisions.md` merged from inbox.
- **Orchestration logged:** `.squad/orchestration-log/20260312T100300Z-architect.md`

