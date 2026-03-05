# Backend History

<!-- Session logs appended by Scribe -->

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
