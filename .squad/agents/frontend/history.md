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
- Home.razor uses `<AuthorizeView>` (not `@attribute [Authorize]`) since it serves both auth and non-auth users; uses `[CascadingParameter] Task<AuthenticationState>` to gate template loading
- Template quick-create buttons on Home page show spinner on the clicked button via `_executingTemplateId` tracking, with dismissible alert feedback
- Templates.razor CRUD page uses inline form fields (not EditForm) with manual validation, matching the project's lightweight approach
- Delete confirmation pattern: track `_deleteConfirmId` to swap the Delete button with Confirm/Cancel pair inline
- Nav icon SVG data URIs must be URL-encoded; lightning-fill icon added as `.bi-lightning-fill-nav-menu` in NavMenu.razor.css
- Task completion toggle pattern: optimistic UI update with rollback on error, `_togglingTaskId` prevents double-clicks, spinner replaces checkbox during API call, dismissible `_toggleError` alert for failures
- Since DTOs are records (immutable), toggling requires rebuilding the list via LINQ `.Select(t => t with { IsCompleted = newStatus })` and reassigning
- Shared `TaskStatusCheckbox` component (`Components/Shared/TaskStatusCheckbox.razor`) encapsulates checkbox/spinner toggle UI, API call, auth redirect, and error handling
- Component uses double-invoke pattern on `OnStatusChanged`: first call for optimistic update, second call (with original status) for rollback on error
- `OnError` EventCallback<string> communicates error messages back to parent pages for page-level alert display
- Razor attribute expressions with null-forgiving operator need parentheses: `@(_selectedListId!)` not `@_selectedListId!`
- Archive/unarchive pattern: track `_archivingListId` to show spinner on the specific list being processed, guard with `if (_archivingListId is not null) return` to prevent double-clicks
- Collapsible section pattern: `_showArchived` bool toggles visibility, `_archivedListsLoaded` bool ensures API is only called on first expand (lazy load)
- When modifying `IReadOnlyList` properties (like `TaskLists`), create new lists via LINQ `.Where().ToList()` or collection expressions `[.. existing, newItem]`
- Bootstrap Icons CSS added via CDN in App.razor (`bootstrap-icons@1.11.3`), enabling `<i class="bi bi-archive">` icon usage throughout the app
- Use `@onclick:stopPropagation="true"` on nested buttons inside clickable list items to prevent parent click handler from firing

## 2026-03-06: Sync Performance Improvements

**Session:** Sync Performance Integration (2026-03-06T0901Z)

### Completed Tasks

1. **Archive/Unarchive UI**
   - Added archive/unarchive buttons to each list item in sidebar
   - Uses `_archivingListId` state tracking to prevent double-clicks
   - Shows spinner on button during API call
   - Calls `SetTaskListArchivedAsync()` on backend
   - On success: list moves between active and archived sections
   - On error: dismissible alert displays error message

2. **Collapsible Archived Section**
   - New "Archived" section in sidebar below active lists
   - Lazy-loads archived lists on first expand via `GetArchivedTaskListsAsync()`
   - Uses `_showArchived` bool for toggle state
   - Uses `_archivedListsLoaded` bool to prevent redundant API calls
   - Chevron icon animates on collapse/expand
   - Visually distinct styling (muted text/icons)

3. **Bootstrap Icons Integration**
   - Added `bootstrap-icons@1.11.3` CSS from jsDelivr CDN to `App.razor`
   - Enables icon classes: `bi-archive`, `bi-arrow-counterclockwise`, `bi-chevron-up`, `bi-chevron-down`
   - Icons rendered via `<i class="bi bi-*"></i>` inline elements

### Cross-Team Coordination

**Backend:** Implemented `IsArchived` flag on `CachedTaskList` entity with filters in sync methods. Parallel `SyncTasksForListsInParallelAsync()` using `Task.WhenAll` + `SemaphoreSlim`. SQLite WAL mode enabled at startup.

### Technical Details

- Optimistic list manipulation: after API success, lists are moved locally (no full reload)
- Selection clearing: when archiving the currently selected list, selection and tasks are cleared
- Component state: `_archivedLists` list tracks archived items separately from `TaskLists`
- Error handling: 401 redirects to OIDC sign-in via `NavigationManager.NavigateTo()`
- Lazy-load pattern prevents unnecessary API calls on page load

### Files Modified

Pages: `Components/Pages/Tasks.razor`  
Layout: `Components/App.razor`

### Build Status

 Project builds clean. UI renders correctly with Bootstrap Icons.

## 2025-07-22: Flowbite Blazor UI Redesign

**Session:** Full UI migration from Bootstrap to Flowbite Blazor + Tailwind CSS

### Completed Tasks

1. **MainLayout.razor** — Replaced Bootstrap layout with Tailwind `flex min-h-screen`. Sticky sidebar + sticky header bar with auth links.
2. **NavMenu.razor** — Flowbite `<Sidebar>`, `<SidebarLogo>`, `<SidebarItem>` with icons (HomeIcon, CalendarMonthIcon, ListIcon, StarIcon, LockIcon).
3. **Home.razor** — Flowbite `<Heading>`, `<Paragraph>`, `<Alert>`, `<Button>` (with `Loading` prop), `<Spinner>`.
4. **Today.razor** — Card-styled div with divide-y items. `<Badge>` for importance, `<Spinner>` for loading.
5. **Tasks.razor** — Tailwind `grid grid-cols-1 md:grid-cols-3` two-panel layout. Archive/restore text buttons. Collapsible archived section.
6. **Templates.razor** — Flowbite `<Card>`, `<Table>` family, `<Button>`, `<Badge>`, `<Alert>`. Native inputs with Tailwind styling.
7. **ApiKeys.razor** — Same pattern as Templates with warning alert for new key display.
8. **TaskStatusCheckbox.razor** — Flowbite `<Spinner>` + Tailwind-styled native checkbox.

### Learnings

- Flowbite Blazor v0.2.6-beta: `Badge`, `Button` are types with nested enums; `Table`, `Typography` are namespaces
- Use `@using static Flowbite.Components.Button` to import `ButtonColor`, `ButtonSize` as nested types
- Use `@using Flowbite.Components.Table` for Table sub-components
- Use native HTML inputs with Tailwind classes for reliable `@bind` behavior
- `<Alert>` uses `<CustomContent>` for rich content; manual close button for dismissibility
- Dark mode: use `dark:` Tailwind variants throughout
