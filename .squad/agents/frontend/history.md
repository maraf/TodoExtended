# Frontend History

<!-- Session logs appended by Scribe -->

## Cross-Team Coordination

**Backend:** Added `GetTodayTasksAsync(CancellationToken)` method to ITodoService returning `IEnumerable<TodoTaskWithList>`. Aggregates tasks from all lists with due date matching today; parses Graph DueDateTime via `DateTimeOffset.Parse()`, uses `DateOnly.FromDateTime()` for timezone-safe comparison.

## Learnings

- Pages follow pattern: `@page`, `@attribute [Authorize]`, `@rendermode InteractiveServer`, `@inject ITodoService`
- Loading/error/empty states use `_loading` bool, `_error` string, and conditional rendering
- Nav links go inside `<AuthorizeView><Authorized>` block in `NavMenu.razor`
- Nav icons use inline SVG data URIs as CSS background images in `NavMenu.razor.css` with `.bi-*-nav-menu` class names
- Task display uses Bootstrap list-group with checkboxes, strikethrough for completed, badge for high importance
- Backend's `TodoTaskWithList` record carries ListId and ListName for subtitle context in task display
- Key file paths: `Components/Pages/Today.razor`, `Components/Layout/NavMenu.razor`, `Services/ITodoService.cs`
- Today page route: `/today`, placed above "My Tasks" in nav for prominence
- PersistentComponentState pattern used on Tasks.razor (key: "taskLists") and Today.razor (key: "todayTasks") to avoid double-fetch during prerender→interactive transition
- Pattern: inject PersistentComponentState, TryTakeFromJson in OnInitializedAsync, RegisterOnPersisting callback, IDisposable for subscription cleanup
- Only persist data fetched during initial load; on-demand data (e.g. tasks for a selected list) is not persisted
