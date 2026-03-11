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

### 2025-07-24 — MudBlazor Redesign Architecture
- Designed full UI migration from Flowbite Blazor + Tailwind CSS to MudBlazor v9 (Material Design).
- **Layout:** MudLayout + MudAppBar + MudDrawer (responsive) + MudMainContent replaces fixed sidebar layout. Adds mobile-friendly collapsible drawer.
- **Task lists:** MudList + MudListItem with MudCheckBox and MudChip replaces raw divs. Richer, more scannable.
- **Tasks page:** MudTabs for Active/Archived replaces collapsible section. MudMenu per list for archive actions replaces inline text buttons.
- **Forms:** MudDialog for create/edit (Templates, API Keys) replaces inline card forms. MudFab for primary "add" action.
- **Tables:** MudDataGrid replaces Flowbite Table for Templates and API Keys pages. Adds free sorting.
- **Feedback:** ISnackbar replaces inline Alert components. Non-blocking, auto-dismiss.
- **Loading:** MudSkeleton replaces Spinner components. Content-shaped placeholders.
- **Theme:** Custom MudTheme with blue primary, purple secondary, teal success. Dark mode toggle in AppBar.
- **Migration order:** Infrastructure → Layout → Shared components → Simple pages → Complex pages → Theme tuning.
- **12 files** need changes; all are rewrites except Program.cs and csproj (additive).

### 2026-03-06 — MudBlazor Redesign Implementation
- Completed full UI migration from Flowbite Blazor + Tailwind CSS to MudBlazor v9 Material Design.
- **Implemented:** All 8 components successfully rewritten (MainLayout, NavMenu, Home, Today, Tasks, Templates, ApiKeys, TaskStatusCheckbox).
- **Build verified:** 0 errors, 0 warnings. All MudBlazor components integrated cleanly.
- **Theme applied:** Custom MudTheme with blue primary (#1976D2), purple secondary (#7C4DFF), teal tertiary (#00BFA5). Dark mode toggle in AppBar.
- **Key patterns:** MudLayout + MudAppBar (responsive header), MudDrawer (collapsible sidebar), MudList (task display), MudDataGrid (templates/API keys), MudDialog (CRUD forms), ISnackbar (feedback), MudSkeleton (loading states).
- **Commit:** `014caf2` — "Redesign UI from Flowbite Blazor to MudBlazor v9"

### 2025-07-25 — Garmin Watch Companion App Architecture
- Designed Garmin Connect IQ companion app for TodoExtended. Selected **Device App** type (not widget/watchface) for full interaction support.
- **Communication path:** Watch → Bluetooth → Garmin Connect Mobile (phone) → HTTPS → TodoExtended REST API. No direct HTTP from watch; phone acts as transparent proxy via `Communications.makeWebRequest()`.
- **Authentication:** Existing API key system (`X-Api-Key` header). Key entered via Garmin app settings on phone — no OAuth needed on watch.
- **v1 scope:** View today's tasks, mark tasks complete, quick-create from templates. No free-text input, no background sync, no offline cache.
- **Project structure:** Monkey C project lives in `garmin/TodoExtended.Watch/` separate from the .NET `src/` folder. Different toolchain (VS Code + Connect IQ SDK + Java), different build system (`monkey.jungle` not MSBuild).
- **Key constraints:** Phone must be paired for any API call; 28-128 KB memory limits; ~8-16 KB response size cap; 1-5s latency per request; 240-454px round screens showing 3-5 list items.
- **No API changes needed for v1** — existing `/api/today`, `/api/templates`, `/api/templates/{id}/execute`, `/api/tasks/{listId}/{taskId}/complete` endpoints are sufficient.
- **Tooling:** Connect IQ SDK, VS Code Monkey C extension (`garmin.monkey-c`), Java JDK 8+.

### 2026-03-06 — Garmin Watch App Scaffold Implementation
- Backend scaffolded the entire Garmin Connect IQ Monkey C companion app project per Architect's design.
- **Deliverables:** 17 files complete — manifest.xml, monkey.jungle, 9 Monkey C source modules (TodoExtendedApp, TodayView/Delegate, TemplatesView/Delegate, TaskDetailView, ApiClient, Settings, Models), resource XMLs (layouts, strings, drawables, settings), and .gitignore.
- **Architecture implemented:** Module-based design with ApiClient, Settings, Models as Monkey C modules; Views/Delegates as classes. WatchUi.Menu2 for memory-efficient list display.
- **Navigation:** Swipe up/down between Today and Templates views. Tap drills into task detail or executes template.
- **Authentication:** Settings-based API key (`X-Api-Key` header); URL and key configurable via Garmin Connect Mobile app — no hardcoding.
- **API coverage:** All 4 endpoints integrated (GET /api/today, GET /api/templates, POST /api/templates/{id}/execute, POST /api/tasks/{listId}/{taskId}/complete). No backend changes required for v1.
- **Error handling:** Covers network failures (-104), HTTP errors, and unconfigured state (missing URL/key).
- **Device targets:** Venu 3, Fenix 7, Forerunner 265. minSdkVersion 4.2.0 for Menu2, Communications, Properties API support.
- **Key decision:** App type `app` (not `widget`) required for Communications permission to make HTTP requests.
- **Status:** Ready for Connect IQ SDK build verification and device testing.

### 2026-03-07 — DbContext Lifetime Bug Fix (Issue #7)
- Backend fixed critical `CachedTodoService` issue where constructor-injected `AppDbContext` was tied to prerender-scope lifetime, causing `ObjectDisposedException` during circuit re-initialization.
- **Solution:** Removed constructor-injected `AppDbContext` parameter; refactored all 7 public and 11 private methods to use `IDbContextFactory<AppDbContext>` exclusively. Each public method creates a fresh, short-lived context. Private methods receive `db` as explicit parameters.
- **Rationale:** `IDbContextFactory` creates contexts independent of any DI scope, ensuring survival past scope disposal. Short-lived contexts prevent stale tracking and memory pressure. Explicit parameter threading maintains data flow visibility.
- **Build:** Clean (0 errors, 0 warnings). Single file changed: `src/TodoExtended.Web/Services/CachedTodoService.cs`. No breaking changes.
- **Alignment:** Implementation follows Architect's analysis and EF Core best practices for Blazor Server.

### 2025-07-25 — Azure AD / Entra ID Configuration Audit
- Audited existing Microsoft Identity Platform setup. The app is ALREADY fully wired for Azure AD authentication.
- **Existing stack:** `Microsoft.Identity.Web` v4.5.0 (OIDC + Graph + UI), API key secondary auth scheme, SQLite-backed distributed token cache, `UserSyncMiddleware`.
- **TenantId:** `consumers` (personal Microsoft accounts). To support work/school accounts, change to `common` or a specific tenant GUID.
- **Graph scopes:** `Tasks.ReadWrite`, `User.Read` — used to access Microsoft To Do via Graph API.
- **Auth patterns in Blazor:** `@attribute [Authorize]` on Tasks, Today, Templates, ApiKeys pages. `<AuthorizeView>` in Home, MainLayout, NavMenu. Routes.razor does NOT use `<AuthorizeRouteView>` — uses `<RouteView>` instead.
- **Secrets management:** `appsettings.local.json` is the intended location for `ClientId` and `ClientSecret` (gitignored). Currently the file only has logging config; user likely uses `dotnet user-secrets` or environment variables.
- **Key file paths:** `Program.cs` (auth registration lines 20-25), `appsettings.json` (AzureAd section), `Authentication/ApiKeyAuthenticationHandler.cs`.

### 2026-03- AI Chat Interface Contract (Issue #22)11 
- Created shared interface contract for AI-powered chat feature on branch `squad/22-ai-chat`.
- **SDK choice:** `Microsoft.Extensions.AI` 10.4.0 + `Microsoft.Extensions.AI.OpenAI` 10.4. provider-agnostic `IChatClient` abstraction.0 
 actions executed.
- **Models:** `ProposedAction`, `ActionConfirmation`, `ActionResult`, `ChatMessage`, `ChatResponse` in `Services/AiChat/AiChatModels.cs`.
- **Interface:** `IChatService` with `SendMessageAsync` (returns text + proposed actions) and `ExecuteActionsAsync` (executes approved actions).
- **Config:** `AiChatOptions` bound to `AiChat`  endpoint, model, API key, max history. Default: GitHub Models `openai/gpt-4.1-mini`.section 
- **Demo mode:** `StubChatService` registered as fallback/demo; real implementation will replace it conditionally.
- **DI:** Scoped `IChatService` registered in `Program.cs`, options bound via `Configure<AiChatOptions>`.
- **6 AI tools planned:** Read: `get_task_lists`, `get_tasks`, `get_today_tasks`. Write: `create_task`, `complete_task`, `uncomplete_task`.
- **Key paths:** `src/TodoExtended.Web/Services/AiChat/` (all 4 files), `Program.cs`, `appsettings.json`.

## 2026-03-11: AI Chat Foundation (Squad #22)

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

