# Decision: Today Page Structure

**By:** Frontend
**Date:** 2025-07-14

## Context
Added a `/today` view showing tasks due today across all task lists.

## Decisions
- Used `TodoTaskWithList` (not `TodoTask`) to display list name alongside each task, since today's tasks span multiple lists
- Placed "Today" nav link above "My Tasks" in sidebar for quick access — it's the more focused, daily-use view
- Used sun icon (`bi-sun-fill`) for the Today nav link to distinguish from the list icon on My Tasks
- Page follows same auth/loading/error patterns as Tasks.razor for consistency
