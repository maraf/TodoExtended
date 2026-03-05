# Delta Query Caching Design

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
