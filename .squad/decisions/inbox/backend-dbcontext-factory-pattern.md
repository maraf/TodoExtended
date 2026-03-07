# Decision: Use IDbContextFactory Exclusively in CachedTodoService

**Author:** Backend  
**Date:** 2026-03-06  
**Status:** Implemented  
**Issue:** #7

## Context

`CachedTodoService` held a constructor-injected `AppDbContext` as a primary constructor parameter. During Blazor Server prerendering, the DI scope that created this DbContext is disposed after the prerender HTTP response completes. When the SignalR circuit connects and Blazor components re-initialize, any code path touching the disposed `db` field throws `ObjectDisposedException`, killing the circuit.

## Decision

Remove the `AppDbContext db` primary constructor parameter from `CachedTodoService`. All database access now goes exclusively through `IDbContextFactory<AppDbContext>`:

- Each **public method** creates a fresh, short-lived context via `await using var db = await dbContextFactory.CreateDbContextAsync();`
- Each **private method** that needs database access receives `AppDbContext db` as an explicit parameter
- `SyncTasksForListsInParallelAsync` and `SyncTasksForListAsync` were already using the factory pattern and were left unchanged

## Rationale

- `IDbContextFactory` creates contexts that are not tied to any DI scope, so they survive scope disposal
- Short-lived contexts per operation prevent stale tracking and reduce memory pressure
- Explicit `db` parameter threading makes the data flow visible and testable
- This is the recommended EF Core pattern for Blazor Server apps

## Impact

- Single file changed: `src/TodoExtended.Web/Services/CachedTodoService.cs`
- No interface changes, no breaking changes to consumers
- All 7 public methods and 11 private methods updated
