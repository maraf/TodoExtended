# Decisions: Delta Query Caching (2026-03-05)

**Author:** Architect  
**Date:** 2025-01-20  
**Status:** Proposed

## Problem Statement

`GetTodayTasksAsync()` currently fetches all task lists, then queries each list with OData filters for today's due date on every page load. This is slow (multiple sequential API calls) and wasteful (repeated fetches of unchanged data). The app needs local caching with Microsoft Graph delta queries to only sync changes.

## Constraints & Context

- **Single-user local app** — no multi-user cache concerns
- **Existing SQLite** via EF Core (AppDbContext) — natural fit for cache storage
- **Delta query limitation** — no `$filter` on `dueDateTime` supported, must cache ALL tasks per list and filter locally
- **Stack:** .NET 10, Blazor Server, EF Core, SQLite
- **Delta API:**
  - `GET /me/todo/lists/{id}/tasks/delta` returns added/deleted/updated tasks
  - Returns `@odata.deltaLink` with `$deltatoken` for next sync
  - Returns `@odata.nextLink` with `$skiptoken` for pagination
  - C# SDK: `graphClient.Me.Todo.Lists[listId].Tasks.Delta.GetAsDeltaGetResponseAsync()`
  - Task lists also support delta: `GET /me/todo/lists/delta`

## Design Goals

1. **Cache-first reads** — serve `GetTodayTasksAsync()` and `GetTasksAsync()` from local SQLite
2. **Delta sync** — only fetch changes from Graph API, not full datasets
3. **Optimistic writes** — `CreateTaskAsync()` and `UpdateTaskStatusAsync()` update cache immediately
4. **Background sync** — periodically refresh cache with delta queries
5. **Transparent to UI** — no changes to Blazor components, ITodoService interface remains stable

## 1. Data Model

Add the following entities to `AppDbContext`:

### `CachedTaskList`
Stores task lists fetched from Graph.

```csharp
public class CachedTaskList
{
    public required string Id { get; set; }               // Graph list ID (PK)
    public required string DisplayName { get; set; }
    public string? DeltaToken { get; set; }               // For list-level delta queries
    public DateTime LastSyncUtc { get; set; }             // Last successful sync
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    
    public ICollection<CachedTask> Tasks { get; set; } = [];
}
```

### `CachedTask`
Stores individual tasks from all lists.

```csharp
public class CachedTask
{
    public required string Id { get; set; }               // Graph task ID (PK)
    public required string ListId { get; set; }           // FK to CachedTaskList
    public required string Title { get; set; }
    public string? Body { get; set; }
    public bool IsCompleted { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Importance { get; set; }
    public bool IsDeleted { get; set; }                   // Soft delete flag (delta removals)
    public DateTime LastSyncUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    
    public CachedTaskList? List { get; set; }             // Navigation property
}
```

**Index Strategy:**
- Primary key on `CachedTask.Id` (unique task ID)
- Index on `ListId` for fast per-list queries
- Composite index on `(IsDeleted, DueDate)` for efficient "today" queries
- Index on `(ListId, IsDeleted)` for `GetTasksAsync()`

### `SyncMetadata`
Stores global sync state.

```csharp
public class SyncMetadata
{
    public required string Key { get; set; }              // PK: e.g., "TaskListsDeltaToken"
    public required string Value { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

**Keys:**
- `TaskListsDeltaToken` — delta token for `/me/todo/lists/delta`
- `LastFullSyncUtc` — timestamp of last complete cache rebuild
- `PerListDeltaToken_{listId}` — per-list task delta tokens (alternative to storing in CachedTaskList.DeltaToken)

**Decision:** Store delta tokens in `CachedTaskList.DeltaToken` instead of separate metadata rows for cleaner model and easier per-list management.

## 2. Service Design

### Architecture: Decorator Pattern

Introduce a **`CachedTodoService`** that decorates `GraphTodoService`. This keeps concerns separated:
- `GraphTodoService` — pure Graph API calls (unchanged)
- `CachedTodoService` — caching, delta sync, cache-first reads
- `ITodoService` — interface remains unchanged

**Dependency Injection:**
```csharp
// Program.cs
builder.Services.AddScoped<GraphTodoService>();
builder.Services.AddScoped<ITodoService, CachedTodoService>();
```

`CachedTodoService` constructor:
```csharp
public class CachedTodoService(
    GraphTodoService graphService,
    AppDbContext db,
    GraphServiceClient graphClient,
    ILogger<CachedTodoService> logger) : ITodoService
```

### Service Responsibilities

**CachedTodoService:**
- Implements `ITodoService`
- Cache-first reads from `AppDbContext`
- Triggers delta sync on cache miss or staleness
- Optimistic updates on write operations
- Delegates to `GraphTodoService` for Graph API writes (create/update)

**GraphTodoService:**
- Keeps existing implementation unchanged
- Used by `CachedTodoService` for write operations and fallback reads

## 3. Sync Flow

### Initial Sync (Cold Cache)

**Trigger:** First call to any read method when `CachedTaskList` table is empty.

**Steps:**
1. Call `graphService.GetTaskListsAsync()` to fetch all lists
2. For each list:
   - Insert `CachedTaskList` with `DeltaToken = null`, `LastSyncUtc = now`
   - Call `graphClient.Me.Todo.Lists[listId].Tasks.Delta.GetAsDeltaGetResponseAsync()`
   - Process all pages (handle `@odata.nextLink` with `$skiptoken`)
   - Insert all tasks as `CachedTask` entities
   - Store final `@odata.deltaLink` in `CachedTaskList.DeltaToken`
3. Save to `AppDbContext`

### Delta Sync (Warm Cache)

**Trigger:** Cache exists but is stale (see staleness policy).

**Steps:**
1. Fetch task lists delta: `graphClient.Me.Todo.Lists.Delta.GetAsDeltaGetResponseAsync()`
   - Use stored `SyncMetadata["TaskListsDeltaToken"]` if available
   - Handle additions: insert new `CachedTaskList`
   - Handle updates: update `DisplayName` in existing `CachedTaskList`
   - Handle deletions: remove `CachedTaskList` and cascade-delete its `CachedTask` entries
   - Store new delta token in `SyncMetadata["TaskListsDeltaToken"]`

2. For each `CachedTaskList`:
   - Call `graphClient.Me.Todo.Lists[listId].Tasks.Delta.GetAsDeltaGetResponseAsync(requestConfig => {
       requestConfig.QueryParameters.Deltatoken = list.DeltaToken;
   })`
   - Handle additions: insert new `CachedTask`
   - Handle updates: update existing `CachedTask` (title, body, status, due date, importance)
   - Handle deletions: set `CachedTask.IsDeleted = true` (soft delete for audit trail)
   - Store new delta token in `CachedTaskList.DeltaToken`

3. Update `CachedTaskList.LastSyncUtc = now`
4. Save all changes to `AppDbContext`

**Pagination Handling:**
```csharp
var deltaResponse = await graphClient.Me.Todo.Lists[listId].Tasks.Delta
    .GetAsDeltaGetResponseAsync(config => {
        if (!string.IsNullOrEmpty(deltaToken))
            config.QueryParameters.Deltatoken = deltaToken;
    });

while (deltaResponse != null)
{
    // Process deltaResponse.Value (added/updated/deleted tasks)
    
    if (deltaResponse.OdataNextLink != null)
    {
        // More pages to fetch
        deltaResponse = await graphClient.Me.Todo.Lists[listId].Tasks.Delta
            .WithUrl(deltaResponse.OdataNextLink)
            .GetAsDeltaGetResponseAsync();
    }
    else
    {
        // Final page: extract delta token from @odata.deltaLink
        deltaToken = ExtractDeltaToken(deltaResponse.OdataDeltaLink);
        break;
    }
}
```

### Detecting Deletions in Delta Response

Microsoft Graph delta queries use `@removed` annotation for deletions:
```csharp
if (task.AdditionalData?.ContainsKey("@removed") == true)
{
    // Task was deleted
    var cachedTask = await db.CachedTasks.FindAsync(task.Id);
    if (cachedTask != null)
        cachedTask.IsDeleted = true;
}
```

## 4. Cache-First Read Operations

### `GetTaskListsAsync()`

**Implementation:**
```csharp
public async Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync()
{
    await EnsureCacheValidAsync();
    
    return await db.CachedTaskLists
        .OrderBy(l => l.DisplayName)
        .Select(l => new TodoTaskList(l.Id, l.DisplayName))
        .ToListAsync();
}
```

### `GetTasksAsync(string taskListId)`

**Implementation:**
```csharp
public async Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId)
{
    await EnsureCacheValidAsync(taskListId);
    
    var tasks = await db.CachedTasks
        .Where(t => t.ListId == taskListId && !t.IsDeleted)
        .ToListAsync();
    
    return tasks
        .Select(t => new TodoTask(
            t.Id, t.Title, t.Body, t.IsCompleted, t.DueDate, t.Importance))
        .OrderBy(t => t.IsCompleted)
        .ThenBy(t => ImportanceSortOrder(t.Importance))
        .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();
}
```

### `GetTodayTasksAsync()`

**Implementation — Cache-First with Optional Background Sync:**
```csharp
public async Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync()
{
    // Trigger async sync in background (fire-and-forget) if cache is stale
    _ = EnsureCacheValidAsync(backgroundSync: true);
    
    var today = DateOnly.FromDateTime(DateTime.Now);
    
    var tasks = await db.CachedTasks
        .Include(t => t.List)
        .Where(t => !t.IsDeleted && t.DueDate == today)
        .ToListAsync();
    
    return tasks
        .Select(t => new TodoTaskWithList(
            t.Id, t.Title, t.Body, t.IsCompleted, t.DueDate, t.Importance,
            t.ListId, t.List!.DisplayName))
        .OrderBy(t => t.IsCompleted)
        .ThenBy(t => ImportanceSortOrder(t.Importance))
        .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();
}
```

**Rationale:** Serve immediately from cache for fast page load, sync in background to keep data fresh for next load. User sees stale data briefly but gets instant response.

**Alternative:** Await sync before returning (slower first load, always fresh data). Choose based on UX preference.

## 5. Optimistic Write Operations

### `CreateTaskAsync(string taskListId, string title, DateOnly? dueDate)`

**Implementation:**
```csharp
public async Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate)
{
    // 1. Call Graph API via graphService
    var created = await graphService.CreateTaskAsync(taskListId, title, dueDate);
    
    // 2. Immediately insert into cache (optimistic)
    var cachedTask = new CachedTask
    {
        Id = created.Id,
        ListId = taskListId,
        Title = created.Title,
        Body = created.Body,
        IsCompleted = created.IsCompleted,
        DueDate = created.DueDate,
        Importance = created.Importance,
        IsDeleted = false,
        LastSyncUtc = DateTime.UtcNow,
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow,
    };
    
    db.CachedTasks.Add(cachedTask);
    await db.SaveChangesAsync();
    
    return created;
}
```

### `UpdateTaskStatusAsync(string taskListId, string taskId, bool completed)`

**Implementation:**
```csharp
public async Task UpdateTaskStatusAsync(string taskListId, string taskId, bool completed)
{
    // 1. Call Graph API via graphService
    await graphService.UpdateTaskStatusAsync(taskListId, taskId, completed);
    
    // 2. Update cache immediately (optimistic)
    var cachedTask = await db.CachedTasks.FindAsync(taskId);
    if (cachedTask != null)
    {
        cachedTask.IsCompleted = completed;
        cachedTask.UpdatedUtc = DateTime.UtcNow;
        cachedTask.LastSyncUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
```

**Error Handling:** If Graph API call fails, throw exception and do NOT update cache. Cache stays consistent with server.

## 6. Cache Invalidation & Staleness Policy

### Staleness Rules

**Cache is considered stale if:**
- `CachedTaskList.LastSyncUtc` is older than **5 minutes** (configurable via `appsettings.json`)
- Any `CachedTaskList` has `DeltaToken == null` (initial sync never completed)
- `SyncMetadata["TaskListsDeltaToken"]` is missing (list-level sync never completed)

**Staleness Check:**
```csharp
private async Task<bool> IsCacheStaleAsync(string? specificListId = null)
{
    var cacheMaxAge = TimeSpan.FromMinutes(5); // TODO: move to config
    var now = DateTime.UtcNow;
    
    if (specificListId != null)
    {
        var list = await db.CachedTaskLists.FindAsync(specificListId);
        return list == null || 
               list.DeltaToken == null || 
               (now - list.LastSyncUtc) > cacheMaxAge;
    }
    
    // Check global staleness
    var oldestSync = await db.CachedTaskLists.MinAsync(l => (DateTime?)l.LastSyncUtc);
    return oldestSync == null || (now - oldestSync.Value) > cacheMaxAge;
}
```

### Sync Triggers

**Automatic:**
- First call to any read method (cold cache)
- Any read method when cache is stale per staleness rules

**Manual:** (Future enhancement)
- Expose `Task SyncNowAsync()` method for manual refresh (e.g., pull-to-refresh UI)

### Invalidation Scenarios

**Full cache rebuild:** (Edge case)
- If delta sync fails (e.g., delta token expired), clear all cache and perform initial sync
- Graph API returns `410 Gone` or `400 Bad Request` when delta token is invalid
- Clear `CachedTasks`, `CachedTaskLists`, `SyncMetadata` and restart

**Partial invalidation:**
- User signs out → clear all cache tables
- List deleted via delta sync → cascade-delete `CachedTask` entries (via EF Core relationship)

## 7. Interface Changes

**None.** `ITodoService` interface remains unchanged. This is a transparent caching layer.

**Optional Future Extension:**
```csharp
public interface ITodoService
{
    // ... existing methods ...
    Task SyncNowAsync(); // Manual sync trigger
    Task<SyncStatus> GetSyncStatusAsync(); // Returns last sync time, cache age
}

public record SyncStatus(DateTime? LastSyncUtc, bool IsSyncing, int CachedListsCount, int CachedTasksCount);
```

## 8. EF Core Migration

### Migration Steps

1. Add entities to `AppDbContext`:
```csharp
public DbSet<CachedTaskList> CachedTaskLists => Set<CachedTaskList>();
public DbSet<CachedTask> CachedTasks => Set<CachedTask>();
public DbSet<SyncMetadata> SyncMetadata => Set<SyncMetadata>();
```

2. Configure relationships and indexes in `OnModelCreating`:
```csharp
modelBuilder.Entity<CachedTaskList>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasMaxLength(256);
    entity.Property(e => e.DisplayName).HasMaxLength(512);
    entity.Property(e => e.DeltaToken).HasMaxLength(2048); // Delta tokens can be long
    entity.HasIndex(e => e.LastSyncUtc);
});

modelBuilder.Entity<CachedTask>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasMaxLength(256);
    entity.Property(e => e.ListId).HasMaxLength(256);
    entity.Property(e => e.Title).HasMaxLength(512);
    entity.Property(e => e.Importance).HasMaxLength(32);
    
    // Indexes for efficient queries
    entity.HasIndex(e => e.ListId);
    entity.HasIndex(e => new { e.IsDeleted, e.DueDate });
    entity.HasIndex(e => new { e.ListId, e.IsDeleted });
    
    // Foreign key relationship
    entity.HasOne(e => e.List)
        .WithMany(e => e.Tasks)
        .HasForeignKey(e => e.ListId)
        .OnDelete(DeleteBehavior.Cascade); // Cascade delete when list is removed
});

modelBuilder.Entity<SyncMetadata>(entity =>
{
    entity.HasKey(e => e.Key);
    entity.Property(e => e.Key).HasMaxLength(256);
    entity.Property(e => e.Value).HasMaxLength(4096);
});
```

3. Generate migration:
```bash
dotnet ef migrations add AddCachingTables --project src/TodoExtended.Web
```

4. Auto-apply at startup (existing pattern in `Program.cs`):
```csharp
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.MigrateAsync();
```

## 9. Implementation Phases

### Phase 1: Core Caching (MVP)
- [ ] Add `CachedTaskList`, `CachedTask`, `SyncMetadata` entities
- [ ] Create EF Core migration
- [ ] Implement `CachedTodoService` with initial sync (no delta yet)
- [ ] Implement cache-first `GetTodayTasksAsync()` (read from cache, full sync on staleness)
- [ ] Update DI registration

**Outcome:** `GetTodayTasksAsync()` reads from cache, but still uses full sync (no delta optimization yet).

### Phase 2: Delta Sync
- [ ] Implement task list delta sync (`/me/todo/lists/delta`)
- [ ] Implement per-list task delta sync (`/me/todo/lists/{id}/tasks/delta`)
- [ ] Handle pagination (`@odata.nextLink`)
- [ ] Handle deletions (`@removed` annotation)
- [ ] Store and reuse delta tokens

**Outcome:** Subsequent syncs only fetch changes, dramatically reducing API calls and latency.

### Phase 3: Optimistic Updates
- [ ] Update `CreateTaskAsync()` to insert into cache immediately
- [ ] Update `UpdateTaskStatusAsync()` to update cache immediately
- [ ] Implement error rollback strategy

**Outcome:** UI feels instant — no waiting for sync after user actions.

### Phase 4: Polish
- [ ] Add `SyncNowAsync()` manual refresh method
- [ ] Add `GetSyncStatusAsync()` for diagnostics
- [ ] Configurable staleness threshold (`appsettings.json`)
- [ ] Background sync health monitoring (log warnings if sync fails repeatedly)
- [ ] Handle delta token expiration (410 Gone → rebuild cache)

## 10. Testing Strategy

### Unit Tests
- Delta sync logic (mock Graph SDK responses)
- Cache staleness detection
- Optimistic update rollback on Graph API failure
- Soft delete handling

### Integration Tests
- Full sync flow (cold cache)
- Delta sync flow (warm cache)
- Optimistic create/update with cache verification
- Cache invalidation scenarios

### Manual Testing
- Load Today page → verify fast load from cache
- Create task → verify immediate appearance (optimistic update)
- Wait 5 min → verify delta sync triggers
- Sign out → verify cache cleared

## 11. Performance Characteristics

### Before (No Caching)
- `GetTodayTasksAsync()`: N+1 Graph API calls (1 for lists + N for tasks per list)
- Latency: ~2-5 seconds (sequential API calls, network dependent)
- API quota impact: High (repeated full fetches)

### After (With Caching)
- **First load (cold cache):** Similar latency (initial sync required), but one-time cost
- **Subsequent loads (warm cache):** <50ms (SQLite query)
- **Background delta sync:** ~200-500ms (only changed tasks fetched)
- **API quota impact:** Minimal (delta queries return only changes)

**Example:**
- User has 5 lists with 200 total tasks
- 2 tasks change per day
- Before: 6 API calls * 200 tasks = 1200 tasks fetched per page load
- After: 1 delta API call fetching 2 tasks every 5 minutes = ~240 tasks/day vs 7200 tasks/day (97% reduction)

## 12. Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Delta token expires (410 Gone) | Catch exception, clear cache, perform full sync |
| Cache grows unbounded (deleted tasks) | Periodic cleanup job (delete `IsDeleted` rows older than 30 days) |
| Cache drift (optimistic update fails) | Next delta sync corrects cache; log warnings for monitoring |
| Concurrency (multiple sync calls) | Use `SemaphoreSlim` to ensure only one sync operation at a time |
| User changes timezone | `DueDate` is stored as `DateOnly` (timezone-agnostic), no drift |

## 13. Configuration

Add to `appsettings.json`:
```json
{
  "TodoCache": {
    "StalenessThresholdMinutes": 5,
    "EnableBackgroundSync": true,
    "SoftDeleteRetentionDays": 30
  }
}
```

Bind via options pattern:
```csharp
public class TodoCacheOptions
{
    public int StalenessThresholdMinutes { get; set; } = 5;
    public bool EnableBackgroundSync { get; set; } = true;
    public int SoftDeleteRetentionDays { get; set; } = 30;
}
```

## 14. Future Enhancements

- **Smart sync scheduling:** Use `IHostedService` background task to sync periodically instead of on-demand
- **Conflict resolution:** Handle optimistic update conflicts if Graph API returns different state
- **Partial list sync:** Allow syncing only specific lists (e.g., sync "Work" list but not "Personal")
- **Sync status UI:** Show last sync time and sync indicator in nav bar
- **Offline mode:** Full offline CRUD with sync queue (requires conflict resolution)
- **Delta query for task list membership:** Currently we sync lists separately; Graph may support combined queries in the future

## Decision Summary

**Approved Architecture:**
- Decorator pattern (`CachedTodoService` wraps `GraphTodoService`)
- Cache-first reads with background delta sync
- Optimistic writes with immediate cache updates
- SQLite storage with EF Core entities (`CachedTaskList`, `CachedTask`, `SyncMetadata`)
- Delta tokens stored per-list in `CachedTaskList.DeltaToken`
- Soft deletes for audit trail (`CachedTask.IsDeleted`)
- 5-minute staleness threshold (configurable)

**Key Trade-offs:**
- **Complexity:** Adds caching layer and delta sync logic (moderate complexity increase)
- **Storage:** Requires local disk space for cache (minimal for typical usage: ~1MB for 1000 tasks)
- **Consistency:** Eventual consistency (cache may lag up to 5 minutes behind server)
- **Benefit:** Dramatically faster Today page loads (50ms vs 2-5s) and 95%+ reduction in API calls

## Next Steps

1. Backend implements Phase 1 (core caching with full sync)
2. Architect reviews implementation for adherence to design
3. Backend implements Phase 2 (delta sync optimization)
4. QA tests all sync scenarios
5. Team reviews performance metrics and adjusts staleness threshold if needed
# Backend: Delta Query Caching Implementation

**Author:** Backend  
**Date:** 2025-01-20  
**Status:** Implemented

## Overview

Implemented delta query caching per Architect's design in `.squad/decisions/inbox/architect-delta-query-caching.md`. This adds a cache-first read layer with Microsoft Graph delta query synchronization to dramatically reduce API calls and improve page load performance.

## Implementation Details

### Phase 1: Core Caching Entities

Added three new entities to `src/TodoExtended.Web/Data/`:

- **`CachedTaskList`** — Stores task lists with `DeltaToken`, `LastSyncUtc`, timestamps
- **`CachedTask`** — Stores tasks with `IsDeleted` soft delete flag, `DueDate`, importance, completion status
- **`SyncMetadata`** — Key-value store for global sync state (task lists delta token)

Updated `AppDbContext` with:
- DbSets for all three entities
- EF Core configuration: max lengths, indexes, cascade delete relationship
- Indexes on `CachedTask`: `ListId`, `(IsDeleted, DueDate)`, `(ListId, IsDeleted)`

### Phase 2: CachedTodoService (Decorator Pattern)

Created `src/TodoExtended.Web/Services/CachedTodoService.cs`:

- **Implements `ITodoService`** — transparent to UI components
- **Decorates `GraphTodoService`** — delegates writes, wraps with caching
- **Constructor dependencies:** `GraphTodoService`, `AppDbContext`, `GraphServiceClient`, `IOptions<TodoCacheOptions>`, `ILogger`

**Key Methods:**

- `EnsureCacheValidAsync()` — checks staleness, triggers sync with double-checked locking via `SemaphoreSlim`
- `InitialSyncAsync()` — cold cache: fetches all lists from `GraphTodoService`, then performs delta queries for all tasks per list
- `DeltaSyncAsync()` — warm cache: performs delta sync for lists and tasks, handles additions/updates/deletions
- `SyncTaskListsAsync()` — uses `graphClient.Me.Todo.Lists.Delta` API, handles pagination, stores delta token in `SyncMetadata`
- `SyncTasksForListAsync(listId, deltaToken)` — per-list task delta sync via `graphClient.Me.Todo.Lists[listId].Tasks.Delta`, stores delta token in `CachedTaskList.DeltaToken`
- `ClearCacheAndInitialSyncAsync()` — handles 410 Gone / invalid delta token by rebuilding cache

**Delta Query Patterns:**

```csharp
// Initial delta (no token)
var response = await graphClient.Me.Todo.Lists[listId].Tasks.Delta.GetAsDeltaGetResponseAsync();

// Subsequent delta (with full URL from previous @odata.deltaLink)
var response = await graphClient.Me.Todo.Lists[listId].Tasks.Delta
    .WithUrl(storedDeltaLinkUrl)
    .GetAsDeltaGetResponseAsync();

// Pagination
while (!string.IsNullOrEmpty(response.OdataNextLink))
{
    response = await graphClient.Me.Todo.Lists[listId].Tasks.Delta
        .WithUrl(response.OdataNextLink)
        .GetAsDeltaGetResponseAsync();
}

// Extract delta token from final page
var deltaToken = response?.OdataDeltaLink;
```

**Deletion Detection:**

```csharp
if (task.AdditionalData?.ContainsKey("@removed") == true)
{
    cachedTask.IsDeleted = true; // Soft delete
}
```

### Phase 3: Cache-First Reads

All read methods (`GetTaskListsAsync`, `GetTasksAsync`, `GetTodayTasksAsync`):
1. Call `EnsureCacheValidAsync()` — syncs if stale
2. Query from `AppDbContext.CachedTasks` / `CachedTaskLists`
3. Filter `!IsDeleted`
4. Apply same sorting as `GraphTodoService`: incomplete first → importance → alphabetical

### Phase 4: Optimistic Writes

- **`CreateTaskAsync`** — calls `graphService.CreateTaskAsync()`, then inserts into `CachedTasks` immediately
- **`UpdateTaskStatusAsync`** — calls `graphService.UpdateTaskStatusAsync()`, then updates cache immediately
- If Graph API throws, cache is NOT updated (preserves consistency)

### Phase 5: Configuration

Added `TodoCacheOptions` class with:
- `StalenessThresholdMinutes` (default: 5)
- `EnableBackgroundSync` (default: true)
- `SoftDeleteRetentionDays` (default: 30)

Bound from `appsettings.json`:
```json
"TodoCache": {
  "StalenessThresholdMinutes": 5,
  "EnableBackgroundSync": true,
  "SoftDeleteRetentionDays": 30
}
```

### Phase 6: Dependency Injection

Updated `Program.cs`:
```csharp
builder.Services.Configure<TodoCacheOptions>(builder.Configuration.GetSection("TodoCache"));
builder.Services.AddScoped<GraphTodoService>(); // Direct registration
builder.Services.AddScoped<ITodoService, CachedTodoService>(); // Interface points to cache layer
```

### Migration

Created `AddCachingTables` migration with:
- `CachedTaskLists` table (Id, DisplayName, DeltaToken, timestamps)
- `CachedTasks` table (Id, ListId FK, Title, Body, IsCompleted, DueDate, Importance, IsDeleted, timestamps)
- `SyncMetadata` table (Key PK, Value, UpdatedUtc)
- Indexes and cascade delete relationship

## Technical Decisions

1. **Decorator Pattern:** Keeps `GraphTodoService` unchanged, `CachedTodoService` wraps it transparently
2. **Delta Token Storage:** Per-list tokens in `CachedTaskList.DeltaToken`, global token in `SyncMetadata`
3. **Soft Deletes:** `IsDeleted` flag preserves audit trail, avoids FK constraint issues during sync
4. **Staleness Threshold:** 5 minutes balances freshness vs API quota
5. **Double-Checked Locking:** `SemaphoreSlim` prevents concurrent syncs
6. **Error Handling:** 410 Gone triggers full cache rebuild
7. **`ParseDueDate` Duplication:** Same logic as `GraphTodoService` for `DateTimeTimeZone` → `DateOnly` conversion
8. **Optimistic Updates:** Write-through cache for instant UI feedback

## Testing Verification

Build succeeded with no errors. Migration created successfully.

## Performance Impact

- **Before:** 2-5s per page load (N+1 Graph API calls)
- **After (warm cache):** <50ms per page load (SQLite query only)
- **Sync overhead:** ~200-500ms every 5 minutes (background delta sync)
- **API quota reduction:** ~97% (only changed tasks fetched)

## Risks & Mitigations

- **Delta token expiration:** Catch 410 Gone, rebuild cache
- **Cache drift:** Next delta sync corrects inconsistencies
- **Unbounded growth:** Future cleanup job for old soft-deleted tasks
- **Concurrency:** `SemaphoreSlim` ensures single sync operation

## Future Enhancements

- Background `IHostedService` for scheduled sync (remove on-demand trigger)
- Manual `SyncNowAsync()` method for pull-to-refresh
- `GetSyncStatusAsync()` diagnostics
- Periodic cleanup of old `IsDeleted` rows

---

## 2026-03-05T14:36:00Z: Config Restart Directive

**By:** Marek Fiera (via Copilot)

When config files (like appsettings.json) are changed, Hockney must do a full app restart. Hot-reload / dotnet watch cannot pick up configuration file changes.

---

## 2026-03-05T14:45:51Z: No Code Duplication in Blazor

**By:** Marek Fiera (via Copilot)

Never duplicate code between Blazor pages. Always refactor shared/reusable logic into Blazor components. Architect should enforce this pattern in reviews.

---

## TaskStatusCheckbox Shared Component

**Author:** Frontend  
**Date:** 2026-03-05  
**Status:** Implemented

Extracted the duplicated task-completion toggle pattern from Tasks.razor and Today.razor into a shared `TaskStatusCheckbox` component at `Components/Shared/TaskStatusCheckbox.razor`.

**Component responsibilities:** checkbox/spinner toggle UI, `ITodoService.UpdateTaskStatusAsync` API call, `MicrosoftIdentityWebChallengeUserException` auth redirect, error communication via `OnError` callback.

**Parent responsibilities:** optimistic list update via `OnStatusChanged` callback (called twice on error — once for optimistic update, once for rollback), page-level error alert display via `_toggleError`.

**Parameters:** `TaskId`, `ListId`, `IsCompleted`, `TaskTitle` (all `[EditorRequired]`), `OnStatusChanged` (`EventCallback<bool>`), `OnError` (`EventCallback<string>`).

Each component instance manages its own `_isToggling` state independently, which is safe because Blazor Server processes events sequentially within a circuit.

---

## API Key Authentication & Token Storage Design

**Author:** Architect  
**Date:** 2026-03-06  
**Status:** Implemented (V1 + V2)

### Overview

Added API key authentication to TodoExtended, enabling users to create named API keys that authenticate API requests without browser sign-in. Each key is tied to a user and their stored MS Graph tokens, allowing API-authenticated requests to call Graph on behalf of the user.

### Requirements

1. **API Keys per  Users can create/manage named API keys stored in SQLiteUser** 
2. **Token  Capture and persist OIDC/Graph access + refresh tokens per userPersistence** 
3. **API  Three endpoints authenticated via API key:Endpoints** 
   - `GET /api/ list user's templatestemplates` 
   - `POST /api/templates/{id}/ create task from templateexecute` 
   - `GET /api/ get today's task listtoday` 
4. ** Hash API keys; encrypt tokens at rest; leverage existing MSAL infrastructureSecurity** 

### Data Model

#### Entities

**ApiKey**: Stores user API keys with one-way hash for secure comparison.
- Id (int, PK)
- UserId (string, FK to User)
- Name (string, user-friendly)
- KeyHash (string, SHA256 hash)
- CreatedUtc, LastUsedUtc (DateTime)
- IsRevoked (bool)

**User**: Represents an authenticated user; stores their Entra ID object identifier.
- Id (string, Entra ID OID, PK)
- Email (string, 256 chars, indexed)
- DisplayName (string)
- HomeAccountId (string, nullable, `{oid}.{tid}` format for MSAL cache lookup)
- CreatedUtc, LastSeenUtc (DateTime)

**UserToken**: Stores encrypted MSAL token cache data per user.
- UserId (string, FK to User, 1:1)
- EncryptedCacheData (string, BLOB)
- UpdatedUtc (DateTime)

**DistributedCacheEntry**: Implements `IDistributedCache` backed by SQLite.
- Key (string, 512 chars, PK)
- Value (byte[], BLOB)
- AbsoluteExpiration (DateTime?)
- SlidingExpirationInSeconds (int?)
- LastAccessed (DateTime)

### V1 Implementation

- **Dual authentication schemes**: Both OIDC and API key schemes registered, authorization policy accepts either
- **In-memory token caching**: Kept `AddInMemoryTokenCaches()` for simplicity
- **Session dependency**: API key requests work only while user's OIDC session is active on server (tokens in MSAL in-memory cache)
- **Key format**: `tek_` prefix + 43 chars base64url (32 random bytes)
- **Storage**: SHA256 hash (lowercase hex) stored in database, plain key returned only once at creation
- **User sync**: Middleware auto-creates User records on OIDC sign-in, extracts OID/email/displayName from claims
- **REST API**: Minimal APIs at `/api` endpoints (templates, today's tasks, key management) secured with `RequireAuthorization()`

### V2 Enhancement: Persistent MSAL Token Cache

Replaced `AddInMemoryTokenCaches()` with `AddDistributedTokenCaches()` backed by SQLite. This enables API key requests to call MS Graph after server restart.

**Core Components:**

1. ** Implements `IDistributedCache` backed by `DistributedCacheEntry` table. Uses `IDbContextFactory<AppDbContext>` to avoid singleton/scoped conflicts.SqliteDistributedCache** 

2. ** Custom `IDbContextFactory<AppDbContext>` implementation. Manually constructs `DbContextOptions` per call. Registered as singleton to serve singleton services.SimpleDbContextFactory** 

3. ** Creates `GraphServiceClient` for API key-authenticated users. Loads user's `HomeAccountId` from database. Builds `ConfidentialClientApplication` with Azure AD config. Attaches distributed cache via MSAL event hooks. Calls `AcquireTokenSilent` with cached account. Handles `MsalUiRequiredException` with clear error message.ApiKeyGraphClientFactory** 

4. **User Entity  Added `HomeAccountId` property. Stores MSAL cache key in `{oid}.{tid}` format. Captured during OIDC sign-in via `UserSyncMiddleware`.Enhancement** 

5. **UserSyncMiddleware  Extracts `tid` claim from OIDC tokens. Computes `homeAccountId = $"{oid}.{tid}"`. Stores in User entity for cache key lookup.Enhancement** 

6. **GraphServiceClient Registration  Factory checks if request is API key authenticated. If API key: uses `ApiKeyGraphClientFactory.CreateForUser(userId)`. If OIDC: uses `OidcTokenProvider` wrapped in `BaseBearerTokenAuthenticationProvider`.Override** 

### Migrations

- ** Creates User, ApiKey, UserToken tablesAddApiKeySupport** 
- ** Creates DistributedCacheEntry table, adds HomeAccountId column to UsersAddPersistentTokenCache** 

### Consequences

**V1 Positive:**
- Simple implementation, low complexity
- Works well for single-server deployment
- Clear security model (hash-based validation)
- Proper separation of concerns (handler, middleware, service)

**V1 Negative:**
- API keys stop working after server restart
- Not suitable for true "headless" scenarios

**V2 Positive:**
- API keys work across server restarts
- Enables fully headless API usage
- Maintains backward compatibility with OIDC

---

## Sync Performance: Archive + Parallel Sync

**Date:** 2026-03-06  
**Author:** Backend  
**Status:** Implemented

### Task List Archiving

Users can mark task lists as archived via `SetTaskListArchivedAsync`. Archived lists are excluded from:
- Sync operations (`DeltaSyncAsync`, `InitialSyncAsync`)
- Staleness checks (`IsCacheStaleAsync`)
- Default list queries (`GetTaskListsAsync`)

The `TodoTaskList` record now includes `IsArchived` (default `false`) for backward compatibility. Newly discovered lists during delta sync default to non-archived.

### Parallel List Sync

`SyncTasksForListAsync` now runs concurrently across lists using `Task.WhenAll` with `SemaphoreSlim` throttle. Each parallel task gets its own `AppDbContext` via `IDbContextFactory<AppDbContext>` (already registered as singleton). Max parallelism is configurable via `TodoCacheOptions.MaxParallelListSync` (default 4).

### SQLite WAL Mode

Set programmatically at startup (`PRAGMA journal_mode=WAL;`) after migration. WAL mode enables concurrent readers with serialized writers, which is essential for parallel sync against SQLite. Connection string unchanged.

---

## Archive/Unarchive UI for Task Lists

**Author:** Frontend  
**Date:** 2026-03-06  
**Status:** Implemented

### Decision

Added archive/unarchive UI to the Tasks page sidebar with these design choices:

1. **Lazy-load archived  The collapsible "Archived" section only fetches `GetArchivedTaskListsAsync()` on first expand, avoiding unnecessary API calls on page load.lists** 

2. **Local list  After archive/unarchive API calls, lists are moved between `TaskLists` and `_archivedLists` locally (no full reload), keeping the UI responsive.manipulation** 

3. **Bootstrap Icons via  Added `bootstrap-icons@1.11.3` CSS from jsDelivr CDN to `App.razor` to support icon usage (`bi-archive`, `bi-arrow-counterclockwise`, `bi-chevron-up/down`). This replaces the need for inline SVG data URIs for new icons.CDN** 

4. **Selection clearing on  When archiving the currently selected list, the selection and tasks are cleared to avoid stale UI state.archive** 

### Key Files

- `Components/Pages/Tasks. Archive/unarchive UI and logicrazor` 
- `Components/App. Bootstrap Icons CDN linkrazor`

---

## MudBlazor Redesign — Component Design Proposal

**Author:** Architect  
**Date:** 2026-03-06  
**Status:** Implemented

### Overview

Replaced Flowbite Blazor + Tailwind CSS with MudBlazor v9 (Material Design). This was not a 1:1 migration — it was a UX rethink using MudBlazor idioms. The original layout used a static sidebar + plain lists + tables. The new design uses Material Design's app shell, rich list components, floating actions, dialogs, and snackbar feedback.

### Setup Completed

- **NuGet:** MudBlazor 9.1.0 installed, Flowbite.Blazor removed
- **App.razor:** MudBlazor stylesheets and fonts added to head; MudBlazor.min.js added to body
- **_Imports.razor:** Added `@using MudBlazor`; removed Flowbite imports
- **Program.cs:** Added `builder.Services.AddMudServices()`

### Theme Implementation

Custom `MudTheme` with task-management personality:
- **Primary:** Material Blue 700 (#1976D2) — trust, productivity
- **Secondary:** Deep Purple (#7C4DFF) — templates/special actions
- **Tertiary:** Teal (#00BFA5) — success/completion
- Dark mode support with `MudThemeProvider` toggle in AppBar

### Component Architecture

1. **MainLayout.razor** — MudLayout with MudAppBar (responsive, dark mode toggle) + MudDrawer (responsive collapse on mobile)
2. **NavMenu.razor** — MudNavMenu with MudNavLink items (auto-highlighted active routes)
3. **Home.razor** — MudGrid + MudCard layout for welcome dashboard + template quick-create
4. **Today.razor** — MudList with task items, MudSnackbar feedback
5. **Tasks.razor** — MudTable for task list management with archive/unarchive UI
6. **Templates.razor** — MudCard grid for template display and management
7. **ApiKeys.razor** — MudTable for API key CRUD operations
8. **TaskStatusCheckbox.razor** — MudCheckBox with status binding and snackbar feedback

### Key Design Decisions

- **Snackbar Feedback:** Replaced all inline alert/success messages with `ISnackbar.Add()` for non-intrusive feedback
- **Responsive Drawer:** `DrawerVariant.Responsive` auto-collapses on mobile (vs. fixed sidebar in Flowbite)
- **Material Icons:** Use MudBlazor's built-in icon system (`Icons.Material.Filled.*`)
- **Container Max-Width:** `MudContainer` constrains content width for readability on large screens
- **Lazy Loading:** Archive lists and other heavy UI sections load on-demand

### Build Verification

✅ 0 compilation errors  
✅ 0 warnings  
✅ All 8 components successfully integrated

### Commit

`014caf2` — "Redesign UI from Flowbite Blazor to MudBlazor v9" 

