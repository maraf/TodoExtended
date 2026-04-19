# Backend History

<!-- Session logs appended by Scribe -->

## Recent Work

### 2026-04-17 — Push Sync Rollout Completed Behind Allowlist

Implemented the actual push-sync rollout path behind `PushSync.Enabled` + `PushSync.AllowedUsers`.

**What landed:**
- Normalized allowlist matching now uses sign-in email / `preferred_username`, not display name.
- Added persisted push-sync state in `SyncMetadata` (subscription IDs, pending refresh state, webhook activity).
- Added a background hosted service that provisions/renews Graph subscriptions and refreshes cache off the request path.
- Added `/api/pushsync/webhook` for Graph validation + notification handling.
- Request-time reads now skip delta sync only when push sync is truly healthy; otherwise they automatically fall back to the existing delta-sync-on-read behavior.

**Validation:**
- `dotnet test tests/TodoExtended.Tests/TodoExtended.Tests.csproj` ✅
- `dotnet test tests/TodoExtended.Components.Tests/TodoExtended.Components.Tests.csproj` ✅
- App startup + webhook validation token endpoint verified locally (`/api/pushsync/webhook?validationToken=ping`) ✅

### 2026-04-17 — MSAL Error Handling Decision & Push Sync Allowlist Ready

**Status: Backend is ready for Push Sync Allowlist implementation**

Decision finalized and merged in `.squad/decisions.md`:
- Config: `PushSync.Enabled` (bool, default false) + `PushSync.AllowedUsers` (email list, case-insensitive)
- Service layer: `IPushSyncGate` injectable gate pattern
- Files to create: `PushSyncOptions.cs`, `IPushSyncGate.cs`, `PushSyncGate.cs`, unit tests
- Files to modify: `appsettings.json`, `Program.cs`
- Injection points: `CachedTodoService` (cache warming gate), future background sync service

**Earlier: MSAL Service Exception Handling (2026-03-13, Status: Implemented)**

When MSAL token acquisition fails with irrecoverable errors (`invalid_client`, expired secrets, revoked consent, 401), users are signed out and redirected to landing page.

**Key Decisions:**
- Two-tier auth error handling in Blazor pages: `MicrosoftIdentityWebChallengeUserException` (re-consent) vs `MsalServiceException` (sign out)
- Helper: `AuthExceptionHelper.IsIrrecoverableMsalError(Exception)` walks full exception chain
- CachedTodoService: explicit `MsalServiceException` catches before ObjectDisposedException

**Files Changed:**
- `Services/AuthExceptionHelper.cs` (new)
- `Services/CachedTodoService.cs` (8 new catches)
- 8 Razor files (19 new catches)

**Impact:** No interface changes. Build clean with `-warnaserror`. All 75 tests pass.

### 2026-03-12 — Explicit userId Parameter Refactoring

Refactored `ITodoService` interface to require explicit `string userId` parameters on all 8 methods, eliminating `IHttpContextAccessor` dependency from `CachedTodoService`.

**Changes:**
- `ITodoService` interface: all 8 methods now require explicit `string userId`
- `CachedTodoService`: removed `IHttpContextAccessor` constructor parameter
- Blazor pages (6 total): extract userId from `CascadingParameter Task<AuthenticationState>`, pass to service calls
- API endpoints (4 total): extract userId from `HttpContext.User` claims
- `ChatService`: extract userId via `IHttpContextAccessor` at HTTP boundary, pass through to ITodoService
- `TemplateService.ExecuteTemplateAsync`: pass userId to ITodoService calls

**Rationale:**
- Eliminates circuit disposal issues in Blazor Server (no HttpContext access inside scoped services)
- Improves testability (no mock IHttpContextAccessor needed)
- Consistent with TemplateService pattern
- Follows Architect guidance on service layer design

**Build & Test:**
- ✅ Build: Clean (0 errors, 0 warnings)
- ✅ Unit Tests: 21 passing
- ✅ No regressions

**Orchestration Log:** `.squad/orchestration-log/20260312T105900Z-backend.md`

### 2026-03-12 — Per-User Data Scoping Implementation Complete

Implemented per-user data isolation across all locally-stored entities following Architect's audit:

**Schema Changes:**
- Added `UserId` (required, string) to TaskTemplate, CachedTaskList, CachedTask
- Added `UserId` (nullable, string) to SyncMetadata for backward compat
- Created EF Core migration 20260312100300_AddUserIdToDataEntities
- Added indexes: CachedTaskList(UserId, IsSynced), CachedTask(UserId, IsDeleted, DueDate), TaskTemplate(UserId)

**Service Layer Changes:**
- **TemplateService:** Switched from AppDbContext DI to IDbContextFactory; all methods require explicit `string userId` parameter
- **CachedTodoService:** Uses IHttpContextAccessor to extract userId internally; all cache queries filter by UserId; per-user delta tokens via `$"TaskListsDeltaToken:{userId}"`; per-user sync locks via `ConcurrentDictionary<string, SemaphoreSlim>`; per-user cache clearing (DELETE WHERE UserId = userId)
- **ChatService:** Passes userId to all ITemplateService calls

**API Endpoint Updates:**
- Template endpoints extract userId from claims (OID claim), pass to service methods

**Blazor Page Updates:**
- Templates.razor, Home.razor: Extract userId from AuthenticationStateProvider claims, pass to service calls
- ApiKeys.razor: Verified pattern consistency (no changes)

**Backward Compatibility:**
- EF Core migration assigns all orphaned existing data to single user (first in Users table)
- Demo mode: templates assigned to "demo-user" identity
- No breaking changes to public APIs

**Build & Test:**
- ✅ Build: Clean (0 errors, 0 warnings)
- ✅ Unit Tests: 21 passing
- ✅ Manual testing: No regressions

**Decision Document:** Merged `.squad/decisions/inbox/backend-user-scoping-impl.md` into `.squad/decisions/decisions.md`; inbox file deleted.

**Orchestration Log:** `.squad/orchestration-log/20260312T100300Z-backend.md`

## Core Context

**Established Implementation & Patterns (pre-2026-03-12):**

1. **UI Framework:** Migrated Bootstrap → Flowbite → MudBlazor v9. All 8 components (MainLayout, NavMenu, Home, Today, Tasks, Templates, ApiKeys, TaskStatusCheckbox) successfully rewritten with MudBlazor v9.1.0.
2. **Service Layer:** GraphTodoService provides raw Graph API access. CachedTodoService wraps it with SQLite cache layer. Methods: GetTaskListsAsync, GetTasksAsync, GetTodayTasksAsync, CreateTaskAsync, UpdateTaskStatusAsync, SetTaskListArchivedAsync.
3. **Delta Caching:** Tracks delta tokens per list, handles pagination, detects soft deletes via @removed, rebuilds cache on 410 Gone. Cache staleness threshold configurable (default 5 min).
4. **API Key Auth:** Custom ApiKeyAuthenticationHandler validates SHA256 hashes. UserSyncMiddleware auto-creates User records on OIDC. ApiKeyService generates base64url keys prefixed `tek_`. Minimal APIs for /api/templates, /api/today, /api/keys.
5. **Token Persistence:** SqliteDistributedCache + ApiKeyGraphClientFactory enable API key users to call Graph API via cached MSAL tokens. Custom ITokenCacheSerializer hooks MSAL token events.
6. **Parallel Sync:** SyncTasksForListsInParallelAsync uses Task.WhenAll + SemaphoreSlim throttle (default 4 concurrent). SQLite WAL mode enabled for concurrent I/O.
7. **DbContext Lifetimes:** IDbContextFactory<AppDbContext> used for short-lived contexts (fixes Blazor Server circuit re-initialization issues). SimpleDbContextFactory singleton provides DbContext to singleton services.
8. **Task Archiving:** IsArchived bool on CachedTaskList. SetTaskListArchivedAsync / GetArchivedTaskListsAsync for CRUD.
9. **Date Handling:** Graph API dueDateTime is dateTimeTimeZone (dateTime string + timeZone). Parsed to DateOnly via ParseDueDate helper to prevent timezone-induced date shifts.
10. **Garmin Watch:** Separate Monkey C project in garmin/TodoExtended.Watch/. Device App communicates via Communications.makeWebRequest(). v1 features: view today tasks, complete tasks, execute templates.



### Service Layer Enhancements

(Detailed learnings moved to Core Context section above)


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
- **Tag task titles:** `garmin/TodoExtended.Watch/source/TagTasksMenu.mc` trims a leading selected tag from task titles for watch display, but only when the tag is a true prefix (`#tag` or `tag`, case-insensitive) followed by end-of-title or a separator like space, `-`, or `:`. This avoids mangling unrelated titles such as longer words that merely start with the same letters.

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

---

### 2026-03-12 — Per-User Data Scoping Implementation Complete

Implemented per-user data isolation across all locally-stored entities following Architect's audit:

**Schema Changes:**
- Added `UserId` (required, string) to TaskTemplate, CachedTaskList, CachedTask
- Added `UserId` (nullable, string) to SyncMetadata for backward compat
- Created EF Core migration 20260312100300_AddUserIdToDataEntities
- Added indexes: CachedTaskList(UserId, IsSynced), CachedTask(UserId, IsDeleted, DueDate), TaskTemplate(UserId)

**Service Layer Changes:**
- **TemplateService:** Switched from AppDbContext DI to IDbContextFactory; all methods require explicit `string userId` parameter
- **CachedTodoService:** Uses IHttpContextAccessor to extract userId internally; all cache queries filter by UserId; per-user delta tokens via `$"TaskListsDeltaToken:{userId}"`; per-user sync locks via `ConcurrentDictionary<string, SemaphoreSlim>`; per-user cache clearing (DELETE WHERE UserId = userId)
- **ChatService:** Passes userId to all ITemplateService calls

**API Endpoint Updates:**
- Template endpoints extract userId from claims (OID claim), pass to service methods

**Blazor Page Updates:**
- Templates.razor, Home.razor: Extract userId from AuthenticationStateProvider claims, pass to service calls
- ApiKeys.razor: Verified pattern consistency (no changes)

**Backward Compatibility:**
- EF Core migration assigns all orphaned existing data to single user (first in Users table)
- Demo mode: templates assigned to "demo-user" identity
- No breaking changes to public APIs

**Build & Test:**
- ✅ Build: Clean (0 errors, 0 warnings)
- ✅ Unit Tests: 21 passing
- ✅ Manual testing: No regressions

**Decision Document:** Merged `.squad/decisions/inbox/backend-user-scoping-impl.md` into `.squad/decisions/decisions.md`; inbox file deleted.

**Orchestration Log:** `.squad/orchestration-log/20260312T100300Z-backend.md`


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

### Explicit userId Refactoring (ITodoService)

Refactored `ITodoService` and all implementations/callers to accept explicit `string userId` parameters instead of relying on `IHttpContextAccessor`:

**Interface Change:** All `ITodoService` methods now require `string userId` — `GetTaskListsAsync(userId)`, `GetTasksAsync(listId, userId)`, `GetTodayTasksAsync(userId)`, `CreateTaskAsync(listId, title, dueDate, userId, reminderTime)`, `UpdateTaskStatusAsync(listId, taskId, completed, userId)`, `SetTaskListSyncedAsync(listId, isSynced, userId)`, `GetNotSyncedTaskListsAsync(userId)`.

**CachedTodoService:** Removed `IHttpContextAccessor` dependency and `GetCurrentUserId()` helper. All methods receive userId from callers. Internal `SyncTasksForListAsync` no longer has optional `userId` fallback.

**GraphTodoService:** Added userId parameter to match interface; parameter ignored since Graph uses token-based auth.

**TemplateService.ExecuteTemplateAsync:** Now passes its existing `userId` parameter through to `ITodoService.CreateTaskAsync`.

**ChatService:** Extracts userId via `GetCurrentUserId()` (from `IHttpContextAccessor`) once per tool/action invocation, passes to all ITodoService calls. ChatService still uses IHttpContextAccessor since it's an infrastructure service at the HTTP boundary.

**Blazor Pages Updated:** Tasks.razor, Today.razor, NavMenu.razor, SyncSettings.razor — added `[CascadingParameter] Task<AuthenticationState>? AuthState` and extract userId from claims. Templates.razor already had userId, just updated ITodoService calls.

**TaskStatusCheckbox.razor:** Added `[CascadingParameter] Task<AuthenticationState>? AuthState` to extract userId for `UpdateTaskStatusAsync`.

**API Endpoints:** All endpoints now extract userId from `HttpContext` via `GetUserId()` helper and pass to ITodoService.

**Tests:** Updated all 21 ChatServiceTests mock signatures; all passing.

**Build:** ✅ `dotnet build -warnaserror` — 0 errors, 0 warnings
**Tests:** ✅ 21/21 passing

## Push Sync Allowlist Rollout with Health-Based Fallback (2026-04-17)

**Status:** Complete  
**Files Added:** 7 service files + webhook endpoint  
**Files Modified:** 6 core files  
**Tests:** All passing

### Learnings

#### Configuration-Gated Features

- **Pattern: Opt-in by default** — Feature flags should disable all new behavior until admin explicitly enables. Use `IOptions<T>` for immutable configuration binding.
- **Email-based allowlist** — Normalize via `StringComparison.OrdinalIgnoreCase` to handle case-insensitive email matching. Store in `appsettings.json` as JSON array.
- **Middleware re-sync** — Update `User.Email` on every OIDC sign-in to catch email changes (e.g., account recovery).

#### State Management Without Schema Changes

- **Pattern: Leverage existing metadata tables** — Store per-user state in `SyncMetadata` table using JSON keys like `PushSync:State:{userId}`. Avoids EF migrations and schema risk late in cycle.
- **SemaphoreSlim for concurrency** — Use `ConcurrentDictionary<string, SemaphoreSlim>` keyed by userId to prevent simultaneous background refreshes on same subscription.
- **Transient failure resilience** — Background service catches all exceptions and records `LastBackgroundHealth` timestamp even on failure (unhealthy state still triggers fallback).

#### Health-Based Fallback Pattern

- **Non-blocking health checks** — Don't throw on health determination. Unhealthy → silently fall back to delta sync (safety-first).
- **Health determination criteria:**
  1. Feature enabled in config
  2. User allowlisted (normalized email match)
  3. Graph subscription exists and not expired
  4. NotificationUrl configured in appsettings
  5. Background service ran recently (< 24h ago)
  6. Last background health = success
- **Fallback trigger:** Any one condition fails → use delta sync on read (existing, proven path).

#### Webhook Integration (Graph API)

- **Validation tokens** — Graph sends validation token in `Notification-Type: subscriptionCreated`. Echo back via `POST /api/pushsync/webhook` with token in response body to complete subscription handshake.
- **Webhook modeling** — Create specific request/response POCO classes for webhook (ChangeNotificationCollection, ResourceData, etc.) to avoid tight coupling to domain models.
- **Mark-for-refresh pattern** — On incoming notification, set `PushSync:Refresh:{userId}` in metadata and let background service handle the actual refresh (decouples webhook latency from user request).

#### Service Registration & DI

- **Hosted services for background work** — Register `PushSyncBackgroundService` as `AddHostedService<T>()` so it auto-starts with the app lifetime.
- **Singleton health service** — Both background service and `CachedTodoService` need `IPushSyncHealthService` — register as singleton so both see same state.
- **Test isolation** — Mock `IHostApplicationLifetime` in tests to avoid actual background service start during test runs.

### Key Implementation Details

**Service Lifecycle:**
1. `Program.cs` binds `PushSync` config section to `PushSyncOptions`
2. Registers `PushSyncGate`, `PushSyncStateStore`, `PushSyncHealthService` as singletons
3. Registers `PushSyncBackgroundService` as hosted service
4. Background service starts on app launch, manages subscription bootstrap/renewal loop

**Health Check Flow:**
1. `CachedTodoService.RefreshCacheAsync()` calls `healthService.IsHealthyAsync(userId)`
2. Health service checks all 6 criteria in parallel
3. If healthy → skip delta sync (push notifications are current)
4. If unhealthy → do delta sync (safe fallback)

**Webhook Flow:**
1. Graph POST `/api/pushsync/webhook` with subscription change notification
2. Endpoint validates Graph signature + returns validation token
3. Sets `PushSync:Refresh:{userId}` flag in state store
4. Background service picks up flag on next loop, refreshes subscription state

### Build & Test

- **Build:** ✅ `dotnet build -warnaserror` — 0 errors, 0 warnings
- **Tests:** ✅ 2 new test files (PushSyncGateTests: 4 scenarios, PushSyncHealthServiceTests: 8 scenarios) + updated CachedTodoServiceTests — all passing
## 2026-04-09: Garmin Watch Tag Trimming — Validation & Duplicate Handoff

**Session:** Garmin Watch Tag Trimming & Orchestration (2026-04-09T11:21:47Z)

### Work Summary

- ✅ **Tag prefix validation** — Completed implementation and validation of Garmin tag trimming feature
- ✅ **Decision merged** — Both Garmin tag trimming and MSAL auth decisions added to decisions.md
- ✅ **Orchestration logged** — Parallel Frontend/Backend agent work documented

### Implementation Context

Parallel implementation with Frontend agent on Garmin TagTasksMenu.mc tag title trimming feature. Backend provided validation and prefix-matching guard logic ensuring:
1. Both #tag and plain 	ag prefixes accepted case-insensitively
2. Prefix must end at title boundary or be followed by lightweight separators ( , -, :)
3. Preserves unrelated words that happen to start with tag text

Final code on disk matches Frontend implementation; no separate artifact produced.

### Outcome

⚠️ **INCONCLUSIVE** — Feature completed via Frontend agent. Backend validation confirms correct implementation.

