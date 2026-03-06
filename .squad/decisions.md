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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
