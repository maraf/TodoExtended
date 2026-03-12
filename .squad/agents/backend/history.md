# Backend History

<!-- Session logs appended by Scribe -->

## Core Context

### Framework Evolution: Bootstrap → Flowbite → MudBlazor

The backend infrastructure underwent two major framework migrations between 2025-07-17 and 2026-03-06:

1. **2025-07-17: Bootstrap → Flowbite Blazor (v0.2.6-beta)**
   - Installed `Flowbite` NuGet package, registered `AddFlowbite()` service
   - Swapped Bootstrap CDN for Tailwind CSS v4 browser build
   - Updated `_Imports.razor` with Flowbite namespaces; `App.razor` with CDN + `<ToastHost/>`
   - Removed Bootstrap scoped CSS (`MainLayout.razor.css`, `NavMenu.razor.css`)
   - Fixed `System.Diagnostics.Activity` ambiguity in Error.razor (Flowbite also defines `Activity` type)
   - Build clean, ready for Frontend redesign

2. **2026-03-06: Flowbite → MudBlazor v9.1.0**
   - Removed Flowbite package, added MudBlazor v9.1.0 via NuGet
   - Updated `_Imports.razor`: removed 7 Flowbite usings, added single `@using MudBlazor`
   - Updated `App.razor`: removed Tailwind/Flowbite setup, added Roboto font + 4 MudBlazor providers (MudThemeProvider, MudPopoverProvider, MudDialogProvider, MudSnackbarProvider)
   - `AddMudServices()` registered in `Program.cs`
   - Build clean

### Service Layer Enhancements

- **`GetTodayTasksAsync(CancellationToken)`:** Aggregates tasks from all lists with due date matching today. Uses OData `$filter` on `dueDateTime` for server-side filtering (reduces payload). Returns `IEnumerable<TodoTaskWithList>` record with list context (ListId, ListName).
- **`CreateTaskAsync(TodoTask, DateOnly?)`:** POSTs tasks to Graph with optional DueDateTime (UTC midnight of given date).
- **`UpdateTaskStatusAsync(listId, taskId, completed)`:** Patches task status via `Me.Todo.Lists[].Tasks[]` with `TaskStatus.Completed` or `NotStarted`.
- **Task Sorting:** Applied consistently across methods — incomplete first → by importance (high/normal/low) → alphabetical fallback. Matches official Microsoft To Do app.
- **Error Handling:** MSAL consent errors (`MicrosoftIdentityWebChallengeUserException`) caught before generic `Exception`. Redirect uses `NavigationManager.NavigateTo("MicrosoftIdentity/Account/SignIn", forceLoad: true)` to break SignalR circuit + force HTTP redirect.

### Delta Query Caching Implementation

Replaces real-time per-call Graph API hits with local SQLite cache + delta sync:

- **Entities:** `CachedTaskList`, `CachedTask`, `SyncMetadata` (in `AppDbContext`)
- **Cache-First Reads:** `CachedTodoService` decorates `GraphTodoService`, routes `GetTasksAsync()` / `GetTodayTasksAsync()` to local cache
- **Delta Sync:** Tracks delta tokens per list, handles pagination (`@odata.nextLink`), detects deletions via `@removed` annotation (soft delete via `IsDeleted` flag), rebuilds cache on 410 Gone
- **Optimistic Writes:** `CreateTaskAsync` / `UpdateTaskStatusAsync` update cache immediately after Graph API success
- **Staleness Threshold:** Configurable via `TodoCacheOptions.CacheStalenessDurationMinutes` (default 5 min)
- **DI Pattern:** `GraphTodoService` registered directly (not via interface), `ITodoService` → `CachedTodoService`
- **EF Core Indexes:** `ListId`, `(IsDeleted, DueDate)`, `(ListId, IsDeleted)` for efficient filtering + cascade delete on list removal

### API Key Authentication System

Complete implementation with SHA256 hashing + token caching:

- **Entities:** `User`, `ApiKey` (hash + revocation), `UserToken` (encrypted MSAL cache)
- **ApiKeyAuthenticationHandler:** Validates keys via SHA256 comparison, creates claims (OID, email), updates last-used timestamp
- **UserSyncMiddleware:** Auto-creates/updates User records on OIDC sign-in, captures `tid` claim for `homeAccountId` computation (`{oid}.{tid}`)
- **ApiKeyService:** Generates 32-byte base64url keys prefixed `tek_`, manages CRUD, returns plain key only at creation
- **Key Routing:** Authorization policy accepts both OIDC and API key schemes
- **Minimal APIs:** `/api/templates`, `/api/templates/{id}/execute`, `/api/today`, `/api/keys` secured with `RequireAuthorization()`
- **V2 - Persistent Token Cache:** `SqliteDistributedCache` (IDistributedCache impl) + `ApiKeyGraphClientFactory` enable API key-authenticated requests to call MS Graph via cached MSAL tokens
- **IDbContextFactory Pattern:** `SimpleDbContextFactory` singleton provides DbContext to singleton services (cache) without scope conflicts

### Task List Archiving

- **Entity:** `IsArchived` bool added to `CachedTaskList`
- **Filtering:** `GetTaskListsAsync`, `IsCacheStaleAsync`, `DeltaSyncAsync` all filter archived lists
- **CRUD:** New `SetTaskListArchivedAsync` / `GetArchivedTaskListsAsync` methods on `ITodoService`
- **DTO:** `TodoTaskList` record carries `IsArchived` (default false, backward compatible)

### Parallel List Sync & Performance

- **`SyncTasksForListsInParallelAsync`:** Uses `Task.WhenAll` + `SemaphoreSlim` throttle (configurable via `MaxParallelListSync`, default 4)
- **SQLite WAL Mode:** Enabled programmatically at startup (`PRAGMA journal_mode=WAL`) for concurrent read/write support needed by parallel sync
- **DbContext Factory:** Each parallel task creates its own DbContext via `IDbContextFactory<AppDbContext>` to avoid thread-safety issues

### Key Technical Patterns

- **Due Date Handling:** Graph API `dueDateTime` is `dateTimeTimeZone` with `dateTime` (string) + `timeZone` fields. Parsed to `DateOnly` via `DateTimeStyles.RoundtripKind` + `DateOnly.FromDateTime()` to prevent timezone-induced date shifts. Helper `ParseDueDate` available in both `GraphTodoService` and `CachedTodoService`.
- **OData Filtering:** Slash notation for complex type properties (`dueDateTime/dateTime ge '2024-01-15T00:00:00'`). Complex `$filter` with parentheses/`or` unreliable; simple `and` conditions work.
- **Error Responses:** REST API uses Results.Ok/BadRequest/NotFound/Unauthorized/NoContent consistent with minimal API conventions.
- **Request DTOs:** Records at bottom of `Program.cs`

### Key Files

- Service: `ITodoService.cs`, `GraphTodoService.cs`, `CachedTodoService.cs`, `ITemplateService.cs`, `TemplateService.cs`
- Data: `AppDbContext.cs`, `CachedTaskList.cs`, `CachedTask.cs`, `TodoCacheOptions.cs`
- Auth: `ApiKeyAuthenticationHandler.cs`, `UserSyncMiddleware.cs`, `ApiKeyService.cs`
- DI/Infrastructure: `Program.cs`, `SimpleDbContextFactory.cs`

---

## 2026-03-06: Flowbite Blazor Migration Complete

**Session:** Flowbite Blazor Setup (2026-03-06T09:33Z)

Infrastructure migration to Flowbite component library complete. All Bootstrap references removed. Tailwind CSS v4 CDN configured. Services registered.

### Completed Tasks

- ✅ Installed Flowbite.Blazor v0.2.6-beta via NuGet
- ✅ Registered `AddFlowbite()` service in Program.cs
- ✅ Swapped Bootstrap CDN for Tailwind CSS v4 browser build (`https://cdn.jsdelivr.net/npm/@@tailwindcss/browser@@4`)
- ✅ Added `<ToastHost />` to App.razor for toast notifications
- ✅ Updated _Imports.razor with Flowbite namespaces
- ✅ Removed all Bootstrap CSS references from app.css
- ✅ Fixed `Activity` type ambiguity by fully qualifying `System.Diagnostics.Activity` in Error.razor

### Cross-Team Coordination

**Frontend:** Simultaneously redesigned all 8 UI files (MainLayout, NavMenu, Home, Today, Tasks, Templates, ApiKeys, TaskStatusCheckbox) with Flowbite components + Tailwind CSS. Breaking change mitigated by parallel execution.

### Technical Details

- Package: Flowbite.Blazor v0.2.6-beta (prerelease, targets net10.0)
- Namespace imports: `Flowbite`, `Flowbite.Components`, `Flowbite.Icons` added globally
- Tailwind v4 browser build via CDN for development (must replace with build pipeline for production)
- Bootstrap scoped CSS (MainLayout.razor.css, NavMenu.razor.css) deleted
- All Bootstrap classes removed from app.css

### Build Status

✅ Clean build, no errors, no warnings

## Cross-Team Coordination

**Frontend:** Today.razor page at `/today` consumes `GetTodayTasksAsync()`. Displays tasks in list-group with completion toggles, high-priority badges, and list name context. Nav link placed top for prominence.

## Learnings

- `TodoTaskWithList` record introduced to carry list context (ListId, ListName) when aggregating tasks across multiple lists. Same field structure as `TodoTask` plus list info.
- Graph API `dueDateTime` is a `dateTimeTimeZone` with separate `dateTime` (string) and `timeZone` fields. The To Do API defaults to UTC when no timezone is specified.
- Due dates are date-only concepts — DTOs use `DateOnly?` (not `DateTimeOffset?`) to prevent timezone-induced date shifts. Parsing uses `DateTime.Parse` with `DateTimeStyles.RoundtripKind` + `DateOnly.FromDateTime()` to extract the date without local timezone conversion.
- The `ParseDueDate` helper in `GraphTodoService` handles the `dateTimeTimeZone` → `DateOnly` conversion.
- The OData filter for "today" uses `DateOnly.FromDateTime(DateTime.UtcNow)` to match UTC-stored dates in Graph.
- Key files: `src/TodoExtended.Web/Services/ITodoService.cs` (interface + DTOs), `src/TodoExtended.Web/Services/GraphTodoService.cs` (Graph implementation).
- Pattern: return empty collection `[]` for null Graph responses; iterate lists to aggregate cross-list results.
- MSAL consent fix (IDW10502): In Blazor Interactive Server, `MicrosoftIdentityWebChallengeUserException` must be caught before generic `Exception` in all Graph API call sites. The fix uses `NavigationManager.NavigateTo("MicrosoftIdentity/Account/SignIn", forceLoad: true)` to break out of the SignalR circuit and trigger a full HTTP redirect to re-authenticate. The `forceLoad: true` is critical — without it, Blazor tries to handle it client-side within the circuit.
- Pages modified for consent handling: `Tasks.razor` (3 catch sites: OnInitializedAsync, SelectList), `Today.razor` (1 catch site: OnInitializedAsync).
- Graph To Do API supports `$filter` on `dueDateTime/dateTime` using `ge`/`lt` for date range queries. The SDK exposes `Filter`, `Select`, `Orderby`, `Top`, `Skip`, `Count`, `Search`, `Expand` on the Tasks endpoint (`TasksRequestBuilderGetQueryParameters`).
- OData filter syntax for complex type properties uses slash notation: `dueDateTime/dateTime ge '2024-01-15T00:00:00'`.
- Complex `$filter` with parentheses and `or` grouping can be unreliable on the To Do API; simple `and` between two conditions works.
- `GetTodayTasksAsync` refactored from client-side to server-side filtering, reducing payload from all tasks to only today's tasks per list.
- Debug logging added to `GraphTodoService` via `ILogger<GraphTodoService>` (primary constructor injection). Logs raw `dueDateTime.DateTime`, `timeZone`, and parsed `DateOnly` in `ParseDueDate`; logs Graph filter string and per-task raw dueDateTime in `GetTodayTasksAsync`; logs per-task raw dueDateTime in `GetTasksAsync`. All at `LogDebug` level to aid due-date troubleshooting without noise in production.
- Task sorting implemented to match official Microsoft To Do app: incomplete first → by importance (high→normal→low) → alphabetical title fallback. Applied consistently to both `GetTodayTasksAsync` and `GetTasksAsync`. Graph API importance values are "High", "Normal", "Low" (from `Importance` enum `.ToString()`); `ImportanceSortOrder` helper uses case-insensitive matching. Sorting is done in-memory after mapping to DTOs.
- EF Core 9.0.7 + SQLite added for local persistence. `AppDbContext` in `TodoExtended.Web.Data` with primary constructor. Auto-migrates at startup in `Program.cs`.
- `TaskTemplate` entity stores title, Graph task list ID/name, DueDateToday flag, and SortOrder. No user ID — single-user local app.
- `CreateTaskAsync` added to `ITodoService`/`GraphTodoService` — POSTs a `TodoTask` to Graph with optional `DueDateTime` (UTC midnight of the given date).
- `ITemplateService`/`TemplateService` provides CRUD + `ExecuteTemplateAsync` which loads a template, computes due date, and delegates to `ITodoService.CreateTaskAsync`.
- `dotnet-ef` global tool needed for migrations — not installed by default on fresh machines.
- EF Core packages: `Microsoft.EntityFrameworkCore.Sqlite` (runtime) + `Microsoft.EntityFrameworkCore.Design` (design-time, PrivateAssets=all).
- `UpdateTaskStatusAsync(taskListId, taskId, completed)` added to `ITodoService`/`GraphTodoService`. Uses `PatchAsync` on `Me.Todo.Lists[].Tasks[]` with `Microsoft.Graph.Models.TaskStatus.Completed` or `NotStarted`. Simple fire-and-forget patch — no return value needed since the UI can optimistically toggle state.
- **Delta Query Caching:** Implemented per Architect's design. Three new entities: `CachedTaskList`, `CachedTask`, `SyncMetadata`. `CachedTodoService` decorates `GraphTodoService` with cache-first reads and Microsoft Graph delta query sync. Tracks delta tokens per list, handles pagination (`@odata.nextLink`), detects deletions via `@removed` annotation (soft delete), and rebuilds cache on 410 Gone. Uses `SemaphoreSlim` to prevent concurrent syncs. Optimistic writes update cache immediately after Graph API success. Staleness threshold configurable via `appsettings.json` (default 5 minutes). Full implementation includes initial cold cache sync and incremental warm cache delta sync.
- Delta query API pattern: initial call without token, subsequent calls use `WithUrl(deltaLink)` where deltaLink is the full URL from `@odata.deltaLink`. Pagination handled via `@odata.nextLink`. Final page contains `@odata.deltaLink` for next sync.
- EF Core indexes on `CachedTask`: `ListId`, `(IsDeleted, DueDate)`, `(ListId, IsDeleted)` for efficient filtering. Cascade delete on `CachedTaskList` removal.
- `ParseDueDate` logic duplicated from `GraphTodoService` into `CachedTodoService` for converting Graph `DateTimeTimeZone` to `DateOnly` during cache sync.
- DI registration changed: `GraphTodoService` registered directly (not via interface), `ITodoService` points to `CachedTodoService`. `TodoCacheOptions` bound from `appsettings.json` via options pattern.
- **API Key Authentication:** Implemented complete system with `User`, `ApiKey`, `UserToken` entities. `ApiKeyAuthenticationHandler` validates keys via SHA256 hash, creates claims including OID, updates last-used timestamps. `UserSyncMiddleware` auto-creates/updates User records on OIDC sign-in. `ApiKeyService` generates 32-byte base64url keys prefixed `tek_`, manages CRUD operations. Authorization policy accepts both OIDC and API key schemes. Minimal API endpoints (`/api/templates`, `/api/templates/{id}/execute`, `/api/today`, `/api/keys`) secured with `RequireAuthorization()`. Token caching kept in-memory for V1 — API calls work while user's OIDC session is active on server. Keys stored as SHA256 hex hashes, plain key returned only once at creation time.
- EF Core entity configurations: User ID max 256 chars, email indexed. ApiKey hash indexed, composite index on (UserId, IsRevoked). UserToken stores EncryptedCacheData as byte[] (BLOB) with 1:1 relationship to User.
- API key format: `tek_` prefix + 43 chars base64url (32 random bytes). Hash computed via SHA256, stored as lowercase hex string.
- REST API patterns: minimal APIs with route groups, `HttpContext.User` claims extraction, Results.Ok/BadRequest/NotFound/Unauthorized/NoContent responses. Request DTOs as records at bottom of Program.cs.

- **V2 - Persistent MSAL Token Cache:** Replaced in-memory token cache with SQLite-backed `IDistributedCache` implementation. MSAL tokens are now persisted to database, enabling API key-authenticated requests to call MS Graph by loading cached refresh tokens. Created `SqliteDistributedCache` (implements `IDistributedCache` using `DistributedCacheEntry` table with expiration support), `ApiKeyGraphClientFactory` (creates `GraphServiceClient` for API key users by loading cached MSAL tokens and calling `AcquireTokenSilent`), and `OidcTokenProvider` (wraps `ITokenAcquisition` for OIDC flow). Added `HomeAccountId` field to User entity to store MSAL cache key (`{oid}.{tid}` format) captured during OIDC sign-in. Program.cs now overrides `GraphServiceClient` registration with factory that routes to API key or OIDC path based on claims. MSAL cache keys stored with user's home account ID prefix in distributed cache. Migration `AddPersistentTokenCache` adds `DistributedCacheEntries` table and `HomeAccountId` column to Users.
- EF Core `IDbContextFactory<T>` singleton pattern: Created `SimpleDbContextFactory` to provide DbContext instances to singleton services (like `SqliteDistributedCache`) without scope conflicts. Factory manually constructs `DbContextOptions` with connection string and returns new contexts per call. Registered as singleton alongside scoped `AddDbContext`.
- Microsoft.Identity.Web distributed cache integration: `AddDistributedTokenCaches()` automatically uses registered `IDistributedCache` for MSAL persistence. Cache entries are binary-serialized MSAL cache data with sliding expiration (90 days). MSAL's `UserTokenCache.SetBeforeAccessAsync` / `SetAfterAccessAsync` hooks load/save from distributed cache using `DeserializeMsalV3` / `SerializeMsalV3`.
- MSAL token acquisition for API keys: Build `ConfidentialClientApplication` manually, attach distributed cache via event hooks, get cached account by `HomeAccountId`, call `AcquireTokenSilent` with Graph scopes. On `MsalUiRequiredException`, fail gracefully with clear error message directing user to sign in again via OIDC.
- `UserSyncMiddleware` enhancements: Now captures `tid` claim from OIDC, computes `homeAccountId` as `{oid}.{tid}`, and stores it in User entity during sign-in. This enables cache key lookup for API key flows.
- **Task List Archiving:** Added `IsArchived` bool to `CachedTaskList` entity. `GetTaskListsAsync` / `IsCacheStaleAsync` / `DeltaSyncAsync` all filter out archived lists. New `SetTaskListArchivedAsync` and `GetArchivedTaskListsAsync` methods on `ITodoService`. `TodoTaskList` record now carries `IsArchived` (default false for backward compat). `GraphTodoService` stubs throw `NotSupportedException` / return empty.
- **Parallel List Sync:** `SyncTasksForListAsync` now accepts an `AppDbContext` parameter. Both `InitialSyncAsync` and `DeltaSyncAsync` call `SyncTasksForListsInParallelAsync` which uses `Task.WhenAll` + `SemaphoreSlim` throttle (configurable via `MaxParallelListSync` in `TodoCacheOptions`, default 4). Each parallel task creates its own `AppDbContext` via `IDbContextFactory<AppDbContext>` to avoid thread-safety issues.
- **SQLite WAL Mode:** Set programmatically at startup in `Program.cs` after migration via `PRAGMA journal_mode=WAL;`. This allows concurrent readers/writers needed for parallel sync. Connection string left unchanged.
- Key files modified: `CachedTaskList.cs`, `AppDbContext.cs`, `ITodoService.cs`, `CachedTodoService.cs`, `GraphTodoService.cs`, `TodoCacheOptions.cs`, `Program.cs`.
- Migration: ` adds `IsArchived` column + index to `CachedTaskLists`.AddTaskListArchiveAndParallelSync` 

## 2026-03-06: Sync Performance Improvements

**Session:** Sync Performance Integration (2026-03-06T0901Z)

### Completed Tasks

1. **Task List Archiving**
   - Added `IsArchived` bool property to `CachedTaskList` entity
   - Updated `GetTaskListsAsync()`, `IsCacheStaleAsync()`, `DeltaSyncAsync()` to filter out archived lists
   - Implemented `SetTaskListArchivedAsync(listId, isArchived)` and `GetArchivedTaskListsAsync()` on `ITodoService`
   - Updated `TodoTaskList` DTO to carry `IsArchived` (backward compatible, defaults to false)
   - Migration: `AddTaskListArchiveAndParallelSync` creates column and index

2. **Parallel List Sync**
   - Refactored `SyncTasksForListAsync()` to accept `AppDbContext` parameter
   - Created `SyncTasksForListsInParallelAsync()` using `Task.WhenAll` + `SemaphoreSlim` throttle
   - Both `InitialSyncAsync()` and `DeltaSyncAsync()` now use parallel sync
   - Max parallelism configurable via `TodoCacheOptions.MaxParallelListSync` (default 4)
   - Each parallel task creates its own `AppDbContext` via `IDbContextFactory<AppDbContext>`

3. **SQLite WAL Mode**
   - Set programmatically at startup in `Program.cs` after migrations
   - Executed via `PRAGMA journal_mode=WAL;` on the connection
   - Enables concurrent readers with serialized writers for safe parallel sync

### Cross-Team Coordination

**Frontend:** Implemented archive/unarchive UI with collapsible archived section and lazy-load on the Tasks.razor page. Bootstrap Icons CDN added to App.razor.

### Technical Details

- `SimpleDbContextFactory` created to provide `DbContext` instances to singleton services without scope conflicts
- Parallel sync leverages `IDbContextFactory<AppDbContext>` singleton pattern for thread-safe concurrent access
- WAL mode is essential for SQLite with concurrent writes from parallel sync tasks
- Backward compatibility maintained: new archived lists default to false in delta sync

### Files Modified

Core: `CachedTaskList.cs`, `AppDbContext.cs`, `ITodoService.cs`, `CachedTodoService.cs`, `GraphTodoService.cs`, `TodoCacheOptions.cs`, `Program.cs`

### Build Status

 Project builds clean. Migration created successfully.

## 2026-03-06: MudBlazor Infrastructure Swap

**Session:** MudBlazor UI Redesign (2026-03-06T09:53:24Z)

### Completed Tasks

1. **Removed Flowbite package** (`Flowbite.Blazor` v0.2.6-beta) and **added MudBlazor** (v9.1.0) via NuGet
2. **Updated App.razor:** Removed Tailwind CDN, Flowbite components, Bootstrap Icons. Added Roboto font link, MudBlazor CSS/JS, and MudBlazor provider components (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`)
3. **Updated _Imports.razor:** Removed all Flowbite `@using` lines, added single `@using MudBlazor`
4. **Updated Program.cs:** Added `builder.Services.AddMudServices()`

### Cross-Team Coordination

**Frontend:** Simultaneously rewrote all 8 UI files with MudBlazor components and Material Design. Zero build errors, zero warnings.

### Technical Details

- MudBlazor v9.1.0 targets net10.0
- All 4 provider components required in App.razor body for full functionality
- CSS/JS served from `_content/MudBlazor/` static assets
- Infrastructure is now ready for Material Design-based UI pages

### Build Status

✅ Infrastructure builds clean. Remaining changes are all in page/layout Razor files (Frontend responsibility).


---

[2025-07-17 Flowbite setup and MudBlazor swap details consolidated into ## Core Context section above]

## Learnings

 Guid Migration (2026-03-06)
- **Pattern:** Replaced autoincrement `int Id` with `Guid Id` (default `Guid.NewGuid()`) on `TaskTemplate` to avoid exposing sequential database identifiers in the API.
- **SQLite constraint:** `ALTER COLUMN` is not supported; migration uses table-rebuild approach (create new table, copy data with generated UUIDs via `randomblob`, drop old, rename).
- **Files touched:** `TaskTemplate.cs`, `AppDbContext.cs` (no changes  `HasKey` works for both types), `ITemplateService.cs`, `TemplateService.cs`, `Program.cs` (API endpoint), `Templates.razor`, `Home.razor`.needed 
- **User preference:** Don't expose autoincrement IDs in APIs; use GUIDs as public identifiers.

## Learnings

### Garmin Connect IQ / Monkey C (2026-03-xx)

- **Project location:** `garmin/TodoExtended.Watch/` — companion watch app for Venu 3, Fenix 7, Forerunner 265.
- **Build system:** `monkey.jungle` uses `project.manifest`, `base.sourcePath`, `base.resourcePath` (property = value format, not JSON/XML).
- **Manifest:** `iq:manifest version="3"` with `iq:application` element; type `app` (not widget) required for Communications permission.
- **Device IDs:** `venu3`, `fenix7`, `fr265` in `<iq:product>` elements.
- **Settings require two files:** `resources/settings/properties.xml` (defaults + types) and `resources/settings/settings.xml` (UI definition). Access via `Application.Properties.getValue("key")`.
- **HTTP:** `Communications.makeWebRequest(url, params, options, method(:callback))` — callback signature is `(responseCode as Number, data as ...)`. Must match `:responseType` to server's Content-Type. Custom headers via `:headers` dictionary.
- **Error codes:** -104 = no phone connection, -300 = network error, -400 = invalid response, -402 = response too large.
- **UI pattern:** `WatchUi.Menu2` with `Menu2InputDelegate` for scrollable lists (auto-scrolls, memory-efficient). Views extend `WatchUi.View`, delegates extend `WatchUi.BehaviorDelegate`.
- **Memory:** 28-128 KB budget depending on device; keep response payloads under 8-16 KB.
- **Launcher icon:** Referenced in `drawables.xml` as `<bitmap id="LauncherIcon">`, must match `launcherIcon="@Drawables.LauncherIcon"` in manifest.

### DbContext Lifetime in Blazor Server (2026-03-xx)

- **Problem:** `CachedTodoService` held a constructor-injected `AppDbContext` as a primary constructor parameter. During Blazor Server prerendering, the DI scope (and its DbContext) is disposed after prerender completes. When the SignalR circuit connects, any code touching the disposed DbContext throws `ObjectDisposedException`, killing the circuit.
- **Fix:** Removed `AppDbContext db` from the primary constructor entirely. All database access now goes through `IDbContextFactory<AppDbContext>.CreateDbContextAsync()` — each public method creates its own short-lived context via `await using var db = ...` and passes it to private methods as a parameter.
- **Key insight:** In Blazor Server, never hold a scoped `DbContext` in a service that outlives the initial request scope. Always use `IDbContextFactory` for on-demand context creation.
- **Unchanged:** `SyncTasksForListsInParallelAsync` and `SyncTasksForListAsync` already used the factory/scoped-db pattern correctly — they were left as-is.

### ObjectDisposedException Handling in Background Sync (2026-03-xx)

- **Problem:** `CachedTodoService` background delta sync (via `EnsureSyncedAsync` → `SyncAsync` → `SyncTaskListsAsync`) calls `GraphServiceClient` which depends on `TokenAcquisition` — all scoped to the Blazor circuit. When the circuit disconnects, the scoped `IServiceProvider` is disposed, and token acquisition throws `ObjectDisposedException`.
- **Fix:** Added `ObjectDisposedException` catch clauses at multiple layers:
  - **Ensure* methods** (`EnsureCacheValidAsync`, `EnsureListsCacheValidAsync`, `EnsureListCacheValidAsync`): catch and silently return, serving stale cache data. This is the outermost safety net.
  - **Sync* methods** (`SyncAsync`, `SyncListsOnlyAsync`): catch and return early, preventing the error from propagating or triggering cache rebuild logic.
  - **Inner methods** (`SyncTaskListsAsync`, `SyncTasksForListAsync`): catch, log as Warning (not Error), and re-throw so outer handlers can terminate the sync chain cleanly.
  - **Batch processing** (`SyncTasksForListsBatchAsync`): catch per-list in the parallel lambda so one disposed scope doesn't crash the entire `Task.WhenAll`.
- **Key insight:** In Blazor Server, scoped services like `GraphServiceClient` / `TokenAcquisition` can be disposed at any time when the circuit disconnects. Background operations that depend on scoped auth services must catch `ObjectDisposedException` and gracefully abort. The next active circuit will trigger a fresh sync.
- **Pattern:** Catch `ObjectDisposedException` before generic `Exception` in catch chains. Log at Warning level (not Error) since it's an expected condition, not a bug.

## Learnings

### AI Chat Service (Issue #22)

- **Architecture:** ChatService uses a manual tool-calling loop instead of FunctionInvokingChatClient. This allows splitting tools into two categories:
  - **Read tools** (get_task_lists, get_tasks, get_today_tasks): auto-invoked, results fed back to AI for reasoning
  - **Write tools** (create_task, complete_task, uncomplete_task): mapped to ProposedAction objects for user confirmation
- **Key files:**
  - `src/TodoExtended.Web/Services/AiChat/ChatService. Real implementation with IChatClient + ITodoServicecs` 
  - `src/TodoExtended.Web/Services/AiChat/DemoChatService. Demo mode canned responsescs` 
  - `src/TodoExtended.Web/Services/AiChat/IChatService. Interface contract (architect-owned)cs` 
  - `src/TodoExtended.Web/Services/AiChat/AiChatModels. DTOs (architect-owned)cs` 
  - `src/TodoExtended.Web/Services/AiChat/StubChatService. Fallback when not configuredcs` 
 StubChatService
- **Microsoft.Extensions.AI API:** Use `AIFunctionFactory.Create(delegate, name, description)` for tool definitions. `FunctionCallContent` and `FunctionResultContent` for the manual tool loop. `OpenAI.Chat.ChatClient` + `AsIChatClient()` extension for creating IChatClient from OpenAI-compatible endpoints.
- **JsonElement handling:** Tool call arguments may arrive as `JsonElement` values; use `element.GetString()` to extract strings.

## 2026-03-11: AI Chat Service Implementation (Squad #22)

**Status:** Complete

Implemented ChatService with manual tool-calling loop and DemoChatService:

**ChatService (280 lines):**
- Manual tool-calling loop (max 10 iterations)
- Read tools auto-invoked (get_task_lists, get_tasks, get_today_tasks)
- Write tools converted to ProposedActions (create_task, complete_task, uncomplete_task)
- FunctionResultContent messages feed data back to AI for reasoning
- ITodoService injected via constructor (scoped)

**DemoChatService (100 lines):**
- Keyword-matched canned responses
- No-op tool execution (demonstrates pattern without API calls)
- Fallback for demo/development environments

**DI Wiring in Program.cs:**
- 3-way conditional registration
 ChatService with real IChatClient
 DemoChatService
 StubChatService

**Decisions:**
- Manual tool loop for full read/write control
- Singleton IChatClient + scoped ChatService/ITodoService
- Stub result for write tools to allow AI to compose appropriate response

** Clean (no errors/warnings)Build:** 

**Orchestration Log:** .squad/orchestration-log/20260311T095047Z-backend.md


## 2026-03-11: Template CRUD in AI Chat Service

**Status:** Complete

Extended AI chat service with full template CRUD capability following the existing pattern:

**Changes to AiChatModels.cs:**
- Added 4 new TaskActionType enum values: CreateTemplate, UpdateTemplate, DeleteTemplate, ExecuteTemplate

**Changes to ChatService.cs:**
- Added ITemplateService to constructor DI
- Added template tools to WriteTools set (create_template, update_template, delete_template, execute_template)
- Updated SystemPrompt to describe template capabilities
- Added read tool: get_templates (returns all templates)
- Added write tool stubs: create_template, update_template, delete_template, execute_template
- Implemented GetTemplatesAsync() to call templateService.GetAllAsync()
- Added "get_templates" case to ExecuteReadTool()
- Extended MapToProposedAction() with template action types
- Extended ExecuteAction() with template operations:
  - CreateTemplate: parse parameters and call templateService.CreateAsync()
  - UpdateTemplate: load by ID, update fields, call templateService.UpdateAsync()
  - DeleteTemplate: call templateService.DeleteAsync()
  - ExecuteTemplate: call templateService.ExecuteTemplateAsync()
- Skipped ValidateIdParameter() for template actions (templates use Guid IDs, not opaque Graph API IDs)
- Added using TodoExtended.Web.Data for TaskTemplate

**Changes to DemoChatService.cs:**
- Added template keyword detection ("template", "templates")
- Added CreateTemplate canned response for "create template" / "new template"
- Added DeleteTemplate canned response for "delete template"
- Added ExecuteTemplate canned response for "execute template" / "run template" / "use template"
- Added template list canned response (shows 3 demo templates)
- Updated default help text to mention template capabilities

**Tool Parameter Design:**
- get_templates: no parameters
- create_template: title, listId, listName, dueDateToday (bool), reminderTime (HH:mm string)
- update_template: templateId (Guid string), all fields optional
- delete_template: templateId (Guid string)
- execute_template: templateId (Guid string)

**Key Learnings:**
- Template IDs are Guids (not opaque Graph API IDs), so ValidateIdParameter() doesn't apply
- TimeOnly? ReminderTime requires parsing from string (HH:mm format)
- TaskTemplate requires TaskListName (display name) in addition to TaskListId
- Template actions follow same ProposedAction confirmation flow as task operations

**Build:** Clean (no errors/warnings)

## 2026-03-12: Per-User Data Scoping Implementation

**Status:** Complete

Implemented full per-user data isolation across all locally-stored entities. Previously, templates, cached task lists, cached tasks, and sync metadata were shared globally across all users.

### Phase 1 — TaskTemplate User Scoping
- Added `UserId` (required) + `User` FK to `TaskTemplate` entity
- Updated `ITemplateService`: all 6 methods now accept explicit `string userId`
- Rewrote `TemplateService` to use `IDbContextFactory<AppDbContext>` (was constructor-injected `AppDbContext`), filter all queries by `UserId`, validate ownership on mutations
- Updated `Templates.razor` and `Home.razor` to extract OID from `AuthenticationStateProvider` and pass to all service calls
- Updated API endpoints (`GET /api/templates`, `POST /api/templates/{id}/execute`) with `GetUserId(HttpContext)` helper
- Updated `ChatService` to inject `IHttpContextAccessor` and pass userId to all template operations

### Phase 2 — Cache + Delta Token User Scoping
- Added `UserId` (required) to `CachedTaskList` and `CachedTask` entities
- Added `UserId` (nullable) to `SyncMetadata` entity
- Updated `CachedTodoService`:
  - Injects `IHttpContextAccessor` to resolve current user
  - Per-user delta token key: `$"TaskListsDeltaToken:{userId}"`
  - Per-user sync lock: `ConcurrentDictionary<string, SemaphoreSlim>` replaces global `SemaphoreSlim`
  - All cache reads filtered by `UserId`
  - All cache writes set `UserId` on new rows
  - `ClearCacheAndInitialSyncAsync` only deletes current user's rows

### Phase 3 — Schema Migration
- Single EF Core migration: `AddUserScopingToAllEntities`
- Adds UserId columns to TaskTemplates, CachedTasks, CachedTaskLists, SyncMetadata
- Data migration: assigns orphaned rows to single existing user (if exactly one exists)
- Renames global `TaskListsDeltaToken` key to per-user format
- Indexes: `{UserId, IsSynced}` on CachedTaskLists, `{UserId, IsDeleted, DueDate}` on CachedTasks, UserId on TaskTemplates
- FK: TaskTemplates → Users with cascade delete

### Key Files Changed
- `src/TodoExtended.Web/Data/TaskTemplate.cs`
- `src/TodoExtended.Web/Data/CachedTaskList.cs`
- `src/TodoExtended.Web/Data/CachedTask.cs`
- `src/TodoExtended.Web/Data/SyncMetadata.cs`
- `src/TodoExtended.Web/Data/AppDbContext.cs`
- `src/TodoExtended.Web/Services/ITemplateService.cs`
- `src/TodoExtended.Web/Services/TemplateService.cs`
- `src/TodoExtended.Web/Services/CachedTodoService.cs`
- `src/TodoExtended.Web/Services/AiChat/ChatService.cs`
- `src/TodoExtended.Web/Components/Pages/Templates.razor`
- `src/TodoExtended.Web/Components/Pages/Home.razor`
- `src/TodoExtended.Web/Program.cs`

## Learnings

### Per-User Data Scoping

- **Pattern: explicit userId for business services, IHttpContextAccessor for infrastructure services.** TemplateService accepts `string userId` in every method. CachedTodoService (implementing ITodoService) uses `IHttpContextAccessor` internally since changing ITodoService would cascade to all consumers.
- **OID claim types:** Always check both `"http://schemas.microsoft.com/identity/claims/objectidentifier"` and `ClaimTypes.NameIdentifier` — the former is from OIDC, the latter from API key auth.
- **Per-user sync locks:** `ConcurrentDictionary<string, SemaphoreSlim>` keyed by userId so users sync independently.
- **SQLite migration:** `AddColumn` with `defaultValue: ""` then `UPDATE ... SET UserId = (SELECT Id FROM Users LIMIT 1)` is simpler than table rebuild for adding required columns.

**Build:** Clean (no errors/warnings)
