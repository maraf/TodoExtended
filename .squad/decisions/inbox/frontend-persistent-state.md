# Decision: Use PersistentComponentState for SSR-to-Interactive Data Transfer

**Author:** Frontend  
**Date:** 2025-07-14  
**Status:** Implemented

## Context

Pages using `InteractiveServer` render mode are prerendered on the server, then re-initialize when the circuit connects. This causes a double-fetch of data — once during SSR and again during interactivity.

## Decision

Use `PersistentComponentState` to serialize data fetched during prerendering into the HTML response, then restore it when the component becomes interactive. This eliminates the redundant service call.

### Pattern
1. Inject `PersistentComponentState`
2. In `OnInitializedAsync`, try `TryTakeFromJson` first; only call the service if no persisted data exists
3. Register a `PersistAsJson` callback via `RegisterOnPersisting`
4. Implement `IDisposable` to dispose the `PersistingComponentStateSubscription`

### Scope
- **Tasks.razor**: Persists `_taskLists` (key: `"taskLists"`). On-demand task loading for a selected list is not persisted.
- **Today.razor**: Persists `_tasks` (key: `"todayTasks"`).

## Rationale

- Reduces unnecessary API calls to Microsoft Graph
- Eliminates visible loading flicker on page transition to interactive mode
- Standard Blazor pattern with no additional dependencies
