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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
