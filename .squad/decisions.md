# Squad Decisions

## Active Decisions

### Task Sorting Order

**Date:** 2025-07-18  
**Author:** Backend  
**Status:** Implemented

Tasks returned by `GetTodayTasksAsync` and `GetTasksAsync` now sort consistently:
1. **Incomplete tasks first**, completed tasks at the bottom
2. **By importance**: high → normal → low
3. **Alphabetically by title** as a tiebreaker (case-insensitive)

Sorting is done in-memory after mapping Graph API responses to DTOs using a shared `ImportanceSortOrder` helper. No DTO or interface changes—purely internal sorting.

### Use DateOnly for To Do Due Dates

**Author:** Backend  
**Date:** 2025-07-15  
**Status:** Implemented

DTOs now use `DateOnly?` instead of `DateTimeOffset?` for `DueDate` (renamed from `DueDateTime`). Parsing uses `DateTimeStyles.RoundtripKind` via a `ParseDueDate` helper. This eliminates timezone-related date shifts and makes the API semantically correct, as due dates in Microsoft To Do are conceptually date-only.

### Use Server-Side OData Filtering for Graph To Do Tasks

**Date:** 2025-07-15  
**Author:** Backend  
**Status:** Implemented

`GetTodayTasksAsync()` now uses the Graph API's `$filter` OData query parameter to filter tasks server-side by `dueDateTime/dateTime` using a date range, reducing payload size and network latency.

### MSAL Consent Exception Handling in Blazor Server Pages

**By:** Backend  
**Date:** 2025-07-18  
**Status:** Implemented

All Blazor pages that call `ITodoService` methods now catch `MicrosoftIdentityWebChallengeUserException` and redirect to `MicrosoftIdentity/Account/SignIn` with `forceLoad: true` to handle token expiration and consent flows in the SignalR circuit.

### TodoTaskWithList Record for Cross-List Task Views

**Author:** Backend  
**Date:** 2025-07-14  
**Status:** Implemented

Introduced a `TodoTaskWithList` record that mirrors `TodoTask` but adds `ListId` and `ListName` fields. `GetTodayTasksAsync()` returns `IReadOnlyList<TodoTaskWithList>` to support the Today view showing tasks from all lists with their parent list names.

### Use PersistentComponentState for SSR-to-Interactive Data Transfer

**Author:** Frontend  
**Date:** 2025-07-14  
**Status:** Implemented

Use `PersistentComponentState` to serialize data fetched during prerendering into the HTML response and restore it when components become interactive. This eliminates redundant service calls. Applied to Tasks.razor (`taskLists` key) and Today.razor (`todayTasks` key).

### Today Page Structure

**By:** Frontend  
**Date:** 2025-07-14  
**Status:** Implemented

Added a `/today` view showing tasks due today across all task lists. Uses `TodoTaskWithList` to display list names, placed "Today" nav link above "My Tasks" in sidebar with a sun icon (`bi-sun-fill`), and follows same auth/loading/error patterns as Tasks.razor.

### Task Templates — Local Storage and Quick-Create

**Author:** Architect, Backend, Frontend  
**Date:** 2026-03-05  
**Status:** Implemented

Users can define task templates locally (SQLite + EF Core) with Title, TaskListId, TaskListName, DueDateToday flag, and SortOrder. Templates appear as quick-create buttons on Home page (ordered by SortOrder) and can be fully managed (CRUD) on a dedicated Templates page. Task creation flows through existing ITodoService.CreateTaskAsync → Graph API. No multi-user support (single-user local app assumption).

**Key Components:**
- Data: TaskTemplate entity, AppDbContext, auto-migration at startup
- Services: ITemplateService + TemplateService (CRUD + ExecuteTemplateAsync)
- UI: Templates.razor (full CRUD), Home.razor (quick-create buttons), NavMenu.razor (Templates link)
- No breaking changes; database file (todoextended.db) excluded via .gitignore

### Flowbite Blazor Infrastructure Setup

**Author:** Backend  
**Date:** 2025-07-17  
**Status:** Implemented

Migrated from Bootstrap to Flowbite Blazor component library with Tailwind CSS for the UI layer.

**Key Decisions:**
1. **Type ambiguity resolution:** Fully qualified `System.Diagnostics.Activity` in Error.razor rather than removing global `@using Flowbite.Components` import (benefits all other pages)
2. **Tailwind CSS v4 via CDN:** Using `https://cdn.jsdelivr.net/npm/@@tailwindcss/browser@@4` (browser build) for development; must be replaced with build pipeline for production
3. **Bootstrap removal is breaking:** All existing Bootstrap CSS classes stopped rendering; Frontend redesign must land alongside or after this change

### Flowbite Blazor UI Redesign

**Date:** 2025-07-22  
**Author:** Frontend  
**Status:** Implemented

Migrated all UI from Bootstrap to Flowbite Blazor components + Tailwind CSS utility classes.

**Key Decisions:**
1. **Native HTML inputs over Flowbite form components** — Used native `<input>`, `<select>`, `<checkbox>` styled with Tailwind classes (matching Flowbite's visual design) instead of form components; provides more reliable `@bind` behavior
2. **`@using static` for nested enums** — Added `@using static Flowbite.Components.Badge` and `@using static Flowbite.Components.Button` to _Imports.razor to bring nested enum types into scope
3. **Card-styled divs for task lists** — Used raw Tailwind card styling for list containers rather than `<Card>` component; gives finer control over padding and item separation
4. **All dark mode compatible** — Every custom Tailwind class includes `dark:` variants

**Impact:** All 8 UI files redesigned, _Imports.razor updated with 4 new Flowbite imports, zero Bootstrap classes remain, build clean (zero errors, zero warnings)

### Use IDbContextFactory Exclusively in CachedTodoService

**Author:** Backend  
**Date:** 2026-03-06  
**Status:** Implemented  
**Issue:** #7

`CachedTodoService` held a constructor-injected `AppDbContext` as a primary constructor parameter. During Blazor Server prerendering, the DI scope that created this DbContext is disposed after the prerender HTTP response completes. When the SignalR circuit connects and Blazor components re-initialize, any code path touching the disposed `db` field throws `ObjectDisposedException`, killing the circuit.

**Decision:** Remove the `AppDbContext db` primary constructor parameter from `CachedTodoService`. All database access now goes exclusively through `IDbContextFactory<AppDbContext>`:

- Each **public method** creates a fresh, short-lived context via `await using var db = await dbContextFactory.CreateDbContextAsync();`
- Each **private method** that needs database access receives `AppDbContext db` as an explicit parameter
- `SyncTasksForListsInParallelAsync` and `SyncTasksForListAsync` were already using the factory pattern and were left unchanged

**Rationale:** `IDbContextFactory` creates contexts that are not tied to any DI scope, so they survive scope disposal. Short-lived contexts per operation prevent stale tracking and reduce memory pressure. Explicit `db` parameter threading makes the data flow visible and testable. This is the recommended EF Core pattern for Blazor Server apps.

**Impact:** Single file changed: `src/TodoExtended.Web/Services/CachedTodoService.cs`. No interface changes, no breaking changes to consumers. All 7 public methods and 11 private methods updated.

### NavMenu Emoji Icon Rendering

**Date:** 2026-03-07  
**Author:** Frontend  
**Status:** Implemented

Task list names in Microsoft To Do can contain a leading Unicode emoji (e.g., "🐶Domeczech"). The nav menu extracts this emoji and displays it as a visual icon prefix, stripping it from the text to avoid duplication.

**Decision:**

- **Emoji extraction** uses `StringInfo.GetTextElementEnumerator()` for grapheme-cluster-safe parsing, with `Rune`-based Unicode range checks to identify emoji characters. This correctly handles multi-byte, surrogate pair, and ZWJ emoji sequences.
- **Rendering approach:** Since MudBlazor's `MudNavLink.Icon` only accepts SVG path strings, the emoji is rendered as a styled `<span class="nav-emoji-icon">` inside the nav link's child content, with CSS isolation matching the Material icon slot dimensions (24×24px, 1.25rem font).
- **URL preservation:** The original `DisplayName` (with emoji) is kept in the Href query string for downstream use.
- **Graceful fallback:** If no leading emoji is detected, the nav link renders plain text with no icon prefix, matching previous behavior.

**Impact:** Files: `NavMenu.razor`, `NavMenu.razor.css` (new). No breaking changes; lists without emoji display identically to before.

### Garmin Connect IQ Watch App Scaffold

**Date:** 2026-03-06  
**Author:** Backend  
**Status:** Implemented

Scaffolded the complete Garmin Connect IQ project at `garmin/TodoExtended.Watch/` with full Monkey C source code, targeting Venu 3, Fenix 7, and Forerunner 265 devices.

**Key Choices:**

1. **App type `app` (not `widget`)** — Required for `Communications` permission (HTTP requests)
2. **WatchUi.Menu2 for all list views** — More memory-efficient than custom drawing; auto-scrolls on device
3. **Module-based architecture** — `ApiClient`, `Settings`, `Models` as Monkey C modules for clean separation. Views/Delegates as classes
4. **Settings via properties.xml + settings.xml** — Users configure `apiBaseUrl` and `apiKey` through Garmin Connect Mobile app
5. **Navigation: swipe up/down** — Today view ↔ Templates view via `onNextPage`/`onPreviousPage`. Tap to drill into task detail or execute template
6. **Error handling** — Covers no-connection (-104), network errors, HTTP status codes, and unconfigured state
7. **Placeholder launcher icon** — 1x1 PNG; needs real icon before Connect IQ Store submission
8. **minSdkVersion 4.2.0** — Supports Menu2, Communications, Properties APIs on target devices

**Files Created:**

- `manifest.xml` — App metadata, permissions, device targets
- `monkey.jungle` — Build configuration
- `source/TodoExtendedApp.mc` — AppBase entry point
- `source/TodayView.mc` + `TodayDelegate.mc` — Today's tasks view + input
- `source/TemplatesView.mc` + `TemplatesDelegate.mc` — Template list + execution
- `source/TaskDetailView.mc` — Task detail + completion (view + delegate in one file)
- `source/ApiClient.mc` — HTTP client wrapper for all 4 API endpoints
- `source/Settings.mc` — Properties accessor for API URL and key
- `source/Models.mc` — TodoTask and Template data classes with JSON parsing
- `resources/` — layouts, strings, drawables, settings (properties.xml + settings.xml)
- `.gitignore` — Excludes bin/ output

**API Endpoints Used:**

- `GET /api/today` — Today's tasks
- `GET /api/templates` — Template list
- `POST /api/templates/{id}/execute` — Create task from template
- `POST /api/tasks/{listId}/{taskId}/complete` — Mark task complete

### TaskTemplate Id: Autoincrement Int → Guid

**Date:** 2026-03-06  
**Author:** Backend  
**Status:** Implemented

Replaced `TaskTemplate.Id` from autoincrement `int` to `Guid` (generated client-side via `Guid.NewGuid()`). The API, service layer, and UI all use Guid identifiers.

**Rationale:**

- Sequential integer IDs leak information (row count, insertion order) and are predictable
- GUIDs are safe to expose publicly and don't reveal database internals
- Aligns with the team preference to not expose autoincrement IDs in the API

**Migration Strategy:**

SQLite doesn't support `ALTER COLUMN`, so the EF Core migration uses a table-rebuild:
1. Create new table with TEXT primary key
2. Copy existing rows with SQLite-generated UUID v4 values (`randomblob`)
3. Drop old table, rename new one

**Impact:** Existing template IDs change. Since templates are local-only data and not referenced externally, this is safe.

### Graceful ObjectDisposedException Handling in CachedTodoService

**Date:** 2026-03-10  
**Author:** Backend  
**Status:** Implemented

`CachedTodoService` performs background delta sync against the Microsoft Graph API via `GraphServiceClient`, which depends on `TokenAcquisition` from Microsoft.Identity.Web. Both are scoped to the Blazor Server circuit. When a circuit disconnects (user navigates away, browser closes, network drops), the circuit's DI scope is disposed. Any in-flight or subsequent sync operations that attempt token acquisition throw `ObjectDisposedException`.

**Decision:** Catch `ObjectDisposedException` at multiple layers in `CachedTodoService` and gracefully abort the sync, serving stale cached data instead of crashing:

1. **Ensure* methods** (outermost): Catch and return silently — the public API methods proceed with whatever cached data is available.
2. **SyncAsync / SyncListsOnlyAsync**: Catch and return early — prevents triggering cache rebuild logic (`ClearCacheAndInitialSyncAsync`) on a disposed scope.
3. **SyncTaskListsAsync / SyncTasksForListAsync**: Catch, log as Warning (not Error), re-throw — stops the sync chain cleanly while avoiding noisy error logs.
4. **SyncTasksForListsBatchAsync parallel lambda**: Catch per-list — isolates failures so one disposed scope doesn't crash the entire `Task.WhenAll`.

All catches log at Warning level since this is an expected Blazor Server lifecycle condition, not a bug.

**Rationale:**
- This is the standard pattern for Blazor Server apps with scoped auth services
- The next active circuit will trigger a fresh sync, so no data is lost
- Stale cache is preferable to a crashed circuit or unhandled exception
- Warning-level logging avoids polluting error logs while maintaining observability

**Impact:** Single file changed: `src/TodoExtended.Web/Services/CachedTodoService.cs`. No interface changes, no breaking changes.

### Header Layout Restructure

**Date:** 2026-03-10  
**Author:** Frontend  
**Status:** Implemented

The application header has been restructured to use a split layout with fixed positioning and independent scroll areas. Page titles and icons are now rendered in the header bar instead of in page bodies.

**Key Changes:**

1. **Split Header Layout**
   - Left section (w-64, desktop only): App logo "To Do (ex)" aligned above sidebar
   - Right section (flex-1): Page icon + title, then user controls (user pill, dark mode, sign out)
   - Full-width gradient background spans both sections

2. **Fixed Header + Scrollable Content**
   - Outer container: `h-screen overflow-hidden flex flex-col`
   - Header: Fixed height (h-14), no scroll
   - Sidebar: `overflow-y-auto` (independent scroll)
   - Main content: `overflow-y-auto` (independent scroll)

3. **Page Title Pattern**
   - Use `SectionContent`/`SectionOutlet` to pass page headers from pages to layout
   - Import required: `@using Microsoft.AspNetCore.Components.Sections` in `_Imports.razor`
   - Each page defines: `<SectionContent SectionName="page-header">...</SectionContent>`
   - Layout renders: `<SectionOutlet SectionName="page-header" />`

4. **Responsive Design**
   - Page icons: `hidden sm:flex` (hidden on mobile)
   - Desktop: Sidebar section visible in header with logo
   - Mobile: Hamburger + logo inline, sidebar as overlay

**Rationale:**
- **Better scroll experience:** Only content scrolls, not the entire page. Header and sidebar stay fixed.
- **Visual consistency:** Page title in header bar matches design patterns seen in modern web apps
- **Clean architecture:** SectionContent/SectionOutlet separates page content from layout chrome
- **Mobile-friendly:** Icons hidden on narrow screens, simplified header layout

**Impact:** All 6 pages (Tasks, Today, Templates, ApiKeys, SyncSettings, Home) updated to use the new pattern. MainLayout.razor also updated. No breaking changes to functionality. Build verified clean (zero errors).

### Shared TaskListSkeleton and TaskStatsBar Components

**Date:** 2026-03-11
**Author:** Frontend
**Status:** Implemented

Extracted two shared Blazor components from duplicated markup in Today.razor and Tasks.razor:

1. **TaskListSkeleton.razor** — Parameterized loading skeleton (row count, gradient, badge placeholder)
2. **TaskStatsBar.razor** — Stats bar with open/done chips and hide-completed toggle using `@bind-HideCompleted` two-way binding

**Key Decisions:**
- TaskStatsBar includes the outer `@if (total > 0)` guard internally, so callers don't need wrapping conditionals
- Callers pass pre-computed `OpenCount`/`CompletedCount` ints rather than the full task list, keeping the component decoupled from specific DTO types
- Null-safe `?.Count() ?? 0` pattern used at call sites for nullable collections
- Default parameter values match Tasks.razor usage (the more common page), Today.razor overrides as needed

**Impact:** ~70 lines of duplicated markup eliminated. Both pages build clean with `-warnaserror`.

### Push Sync Allowlist Configuration

**Date:** 2026-04-17  
**Author:** Architect  
**Status:** Proposed

Push synchronization (background cache warming, and later Graph webhooks) needs to be gated behind a user allowlist during rollout. The app uses Microsoft Entra ID (consumer tenant) and stores four identity fields per user: `Id` (OID), `Email`, `DisplayName`, and `HomeAccountId`.

**Key Decisions:**

1. **Match on `Email` (preferred_username / UPN)**
   - Recommended identifier: `Email` — the `User.Email` field, sourced from the `ClaimTypes.Email` or `preferred_username` claim
   - Human-readable for admins editing config
   - Stable for Microsoft consumer accounts (personal MSA)
   - Already synced on every OIDC sign-in by `UserSyncMiddleware`
   - Mitigated edge case: if user changes their MSA email, old config entry stops matching; new one must be added. This is acceptable and provides an implicit "off switch."

2. **Configuration shape**
   ```json
   {
     "PushSync": {
       "Enabled": false,
       "AllowedUsers": []
     }
   }
   ```
   - `Enabled: false` — global kill switch; when false, push sync is off for everyone regardless of the list
   - `AllowedUsers: []` — empty list means nobody gets push sync even if Enabled is true. Populated with email addresses.
   - The list is case-insensitive (use `StringComparer.OrdinalIgnoreCase`)

3. **Options class**
   ```csharp
   namespace TodoExtended.Web.Services;

   public class PushSyncOptions
   {
       public const string SectionName = "PushSync";
       public bool Enabled { get; set; }
       public List<string> AllowedUsers { get; set; } = [];
   }
   ```
   Bound in `Program.cs` via `builder.Services.Configure<PushSyncOptions>(builder.Configuration.GetSection(PushSyncOptions.SectionName));`

4. **Gate service**
   Create a small `IPushSyncGate` / `PushSyncGate` service that answers "should this user get push sync?":
   ```csharp
   public interface IPushSyncGate
   {
       bool IsEligible(string userEmail);
   }
   ```
   Implementation checks `PushSyncOptions.Enabled && AllowedUsers contains email (case-insensitive)`. Injected where push sync is triggered.

5. **Files to change**
   - `appsettings.json` — Add `PushSync` section
   - `Services/PushSyncOptions.cs` — New — options POCO
   - `Services/IPushSyncGate.cs` — New — interface
   - `Services/PushSyncGate.cs` — New — implementation
   - `Program.cs` — Bind options, register gate as singleton
   - Background sync service (future) — Inject `IPushSyncGate`, check before syncing for a user
   - `CachedTodoService.cs` — If soft-stale async refresh is added, gate it behind `IPushSyncGate`

**Testing:**
- Unit test `PushSyncGate` with: enabled+listed, enabled+not-listed, disabled+listed, empty list, case mismatch
- No DB or Graph dependency — pure config-based logic

**Impact:** Zero behavior change at merge (Enabled defaults to false). Clean gate for all future push sync features (background warmer, webhooks, SignalR notifications). Easy to promote to a DB-backed allowlist later if needed (replace `PushSyncGate` implementation, keep interface).

### MsalServiceException Handling — Sign Out on Irrecoverable Auth Failures

**Date:** 2026-03-13  
**Author:** Backend  
**Status:** Implemented

When MSAL token acquisition fails with an irrecoverable `MsalServiceException` (e.g. `invalid_client`, expired secrets, revoked consent, 401 status), the user is now signed out and redirected to the landing page — instead of being left on a broken page with console warnings.

**Key Decisions:**

1. **Two-tier auth error handling in Blazor pages:**
   - `MicrosoftIdentityWebChallengeUserException` → redirect to `MicrosoftIdentity/Account/SignIn` (re-consent, existing behavior)
   - `MsalServiceException` (irrecoverable) → redirect to `MicrosoftIdentity/Account/SignOut` (clear broken session)
   - MSAL catch is evaluated first via exception filter ordering

2. **Helper: `AuthExceptionHelper.IsIrrecoverableMsalError(Exception)`** — Walks the full exception chain (including `AggregateException` inner exceptions) checking for `MsalServiceException` with `ErrorCode == "invalid_client"` or `StatusCode == 401`. This handles cases where MSAL errors are wrapped by Graph SDK or other middleware.

3. **CachedTodoService: explicit `MsalServiceException` catches** — Added before existing `ObjectDisposedException` and generic catches in all sync methods. These log at Warning level and re-throw (not swallow), so the error propagates to the Blazor page for sign-out redirect. This prevents the `ShouldRebuildCache` logic from running on auth failures (which would just fail again).

4. **Demo mode unaffected** — Demo mode doesn't use MSAL, so `MsalServiceException` is never thrown. The new catch blocks are inert in demo mode.

**Files Changed:**
- `Services/AuthExceptionHelper.cs` (new) — Static helper for MSAL error detection
- `Services/CachedTodoService.cs` — 8 new `MsalServiceException` catch blocks
- 8 Razor files — 19 new MSAL catch blocks (NavMenu, Tasks, Today, Home, Templates, ApiKeys, SyncSettings, TaskStatusCheckbox)

**Impact:** No interface changes. Build clean with `-warnaserror`. All 75 tests pass (21 unit + 54 component).

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
