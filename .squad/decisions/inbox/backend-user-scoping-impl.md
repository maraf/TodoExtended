# Decision: Per-User Data Scoping Implementation

**Author:** Backend  
**Date:** 2026-03-12  
**Status:** Implemented

---

## Summary

All locally-stored entities (TaskTemplate, CachedTaskList, CachedTask, SyncMetadata) now include a `UserId` column. All queries filter by the current user's ID. Templates and cache data are fully isolated between users.

## Key Decisions

### 1. Explicit userId for TemplateService, IHttpContextAccessor for CachedTodoService

`ITemplateService` methods accept explicit `string userId` — callers (Blazor pages, API endpoints, ChatService) are responsible for extracting and passing it. This follows the ApiKeyService pattern.

`CachedTodoService` uses `IHttpContextAccessor` internally because it implements `ITodoService`, which is consumed across the entire app. Adding userId to ITodoService would have been a massive cascading change with no benefit (the Graph API already enforces user isolation at the data source level; the cache just needs to tag data).

### 2. TemplateService switched from AppDbContext to IDbContextFactory

Following the same pattern as CachedTodoService (documented in decisions.md), TemplateService now creates short-lived contexts via `IDbContextFactory<AppDbContext>`. This prevents `ObjectDisposedException` in Blazor Server circuits.

### 3. Per-user sync locks

Replaced `static SemaphoreSlim` with `static ConcurrentDictionary<string, SemaphoreSlim>` keyed by userId. Users sync independently — one user's sync doesn't block another's.

### 4. Per-user delta tokens

Delta token key changed from `"TaskListsDeltaToken"` to `$"TaskListsDeltaToken:{userId}"`. Migration renames existing key for backward compatibility.

### 5. SyncMetadata.UserId is nullable

SyncMetadata uses a nullable UserId (vs required on other entities) for backward compatibility. The primary key remains just `Key`, which is now per-user by convention (e.g., `"TaskListsDeltaToken:abc123"`).

## Impact

- 12 files changed
- Single EF Core migration handles all schema changes
- Demo mode works (demo templates assigned to "demo-user")
- Build: zero errors, zero warnings
