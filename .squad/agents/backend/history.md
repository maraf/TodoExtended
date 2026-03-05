# Backend History

<!-- Session logs appended by Scribe -->

## Cross-Team Coordination

**Frontend:** Today.razor page at `/today` consumes `GetTodayTasksAsync()`. Displays tasks in list-group with completion toggles, high-priority badges, and list name context. Nav link placed top for prominence.

## Learnings

- `TodoTaskWithList` record introduced to carry list context (ListId, ListName) when aggregating tasks across multiple lists. Same field structure as `TodoTask` plus list info.
- Graph API DueDateTime is parsed via `DateTimeOffset.Parse(t.DueDateTime.DateTime)` — the `.DateTime` property is a string, not a `DateTime`.
- Date-only comparison uses `DateOnly.FromDateTime()` against `DateTime.Today` to avoid timezone edge cases with `DateTimeOffset`.
- Key files: `src/TodoExtended.Web/Services/ITodoService.cs` (interface + DTOs), `src/TodoExtended.Web/Services/GraphTodoService.cs` (Graph implementation).
- Pattern: return empty collection `[]` for null Graph responses; iterate lists to aggregate cross-list results.
- MSAL consent fix (IDW10502): In Blazor Interactive Server, `MicrosoftIdentityWebChallengeUserException` must be caught before generic `Exception` in all Graph API call sites. The fix uses `NavigationManager.NavigateTo("MicrosoftIdentity/Account/SignIn", forceLoad: true)` to break out of the SignalR circuit and trigger a full HTTP redirect to re-authenticate. The `forceLoad: true` is critical — without it, Blazor tries to handle it client-side within the circuit.
- Pages modified for consent handling: `Tasks.razor` (3 catch sites: OnInitializedAsync, SelectList), `Today.razor` (1 catch site: OnInitializedAsync).
