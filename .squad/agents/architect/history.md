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
