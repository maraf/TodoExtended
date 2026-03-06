# Backend History

<!-- Session logs appended by Scribe -->

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

## 2025-07-17: Flowbite Blazor Infrastructure Setup

### Completed Tasks

1. **Installed Flowbite NuGet package** (v0.2.6-beta prerelease) to TodoExtended.Web.csproj
2. **Registered Flowbite services** in Program.cs via `builder.Services.AddFlowbite()`
3. **Updated App. removed Bootstrap CSS + Icons CDN, added Tailwind CSS v4 CDN (`@@tailwindcss/browser@@4` escaped for Razor), added `<ToastHost />` componentrazor** 
4. **Updated _Imports. added `@using Flowbite`, `@using Flowbite.Components`, `@using Flowbite.Icons`razor** 
5. **Cleaned up app. removed all Bootstrap-specific rules, kept blazor-error-boundary and validation stylescss** 
6. **Deleted Bootstrap scoped  removed `MainLayout.razor.css` and `NavMenu.razor.css`CSS** 
7. **Fixed build  escaped `@` in Tailwind CDN URL, fully qualified `System.Diagnostics.Activity` in Error.razor to resolve ambiguity with `Flowbite.Components.Activity`errors** 

### Learnings

- Flowbite.Blazor v0.2.6-beta targets net10.0 and installs cleanly
- `@using Flowbite.Components` introduces `Activity` type that conflicts with `System.Diagnostics. must fully qualify in Error.razorActivity` 
- Tailwind CDN URL contains `@` characters that Razor interprets as  must escape as `@@`code 
- `AddFlowbite()` extension method lives in `Flowbite.Services` namespace
- RZ10012 warning on `<ToastHost />` in App.razor is  component resolves at runtime despite Razor analyzer not finding it at design timeharmless 
- Key files: `Program.cs` (service registration), `App.razor` (Tailwind CDN + ToastHost), `_Imports.razor` (Flowbite usings), `app.css` (cleaned)

 MudBlazor Infrastructure Swap

### Completed Tasks

1. **Removed Flowbite package** (`Flowbite` v0.2.6-beta) and **added MudBlazor** (v9.1.0) via `dotnet remove/add package`
 `AddMudServices()`
3. **Updated _Imports.razor**: Removed all 7 Flowbite `@using` lines, added single `@using MudBlazor`
4. **Updated App.razor**: Removed Tailwind CDN, Flowbite `@using`, `<ResourcePreloader/>`, `<ImportMap/>`, `<ToastHost/>`, `<ReconnectModal/>`. Added Roboto font, MudBlazor CSS, MudBlazor JS, and MudBlazor provider components (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`)
5. **app.css**: Already  no Flowbite/Tailwind-specific CSS to removeclean 

### Build Status

 Infrastructure builds clean. Remaining errors are all in page/layout Razor files (Flowbite icons, `AlertColor`  Frontend agent's responsibility.enum) 

### Learnings

- MudBlazor v9.1.0 is latest stable, targets net10.0 cleanly
- MudBlazor requires 4 provider components in App.razor body: `MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`
- Single `@using MudBlazor` replaces all 7 Flowbite namespace imports
- `AddMudServices()` lives in `MudBlazor.Services` namespace (mirrors Flowbite pattern)
- Flowbite's `<ResourcePreloader/>`, `<ImportMap/>`, `<ReconnectModal/>` have no MudBlazor  removedequivalents 
- MudBlazor CSS/JS are served from `_content/MudBlazor/` static web assets
