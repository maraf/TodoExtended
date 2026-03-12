# Design: Per-User Data Scoping Audit & Remediation

**Author:** Architect  
**Date:** 2026-03-12  
**Status:** Proposed  
**Requested by:** Marek Fišera

---

## 1. Problem Statement

TodoExtended was originally designed as a single-user local app. With the introduction of API key authentication and multi-user support (Users table, ApiKey entity), several data stores were never updated to scope data by user. This means:

- **Templates** are shared across all users (no UserId column)
- **Cached task lists and tasks** are shared across all users (no UserId column)
- **Sync metadata (delta tokens)** is stored globally, not per-user
- **Cache clearing** wipes all users' data indiscriminately

The Graph API itself enforces per-user isolation (each user's token only returns their data), but the **local SQLite cache** and **template storage** do not.

---

## 2. Entity Audit — Current User-Scoping Status

| Entity | Has UserId? | User-Scoped? | Risk Level | Notes |
|--------|:-----------:|:------------:|:----------:|-------|
| `User` | N/A (root) | N/A | — | Identity root entity. PK = Entra OID. |
| `ApiKey` | ✅ Yes | ✅ Yes | ✅ Safe | FK to User, all queries filter by UserId. |
| `UserToken` | ✅ Yes (PK) | ✅ Yes | ✅ Safe | One-to-one with User, PK is UserId. |
| `TaskTemplate` | ❌ No | ❌ **Shared** | 🔴 Critical | All users see/execute all templates. |
| `CachedTaskList` | ❌ No | ❌ **Shared** | 🔴 Critical | Cache conflates data from all users. |
| `CachedTask` | ❌ No | ❌ **Shared** | 🔴 Critical | Inherits scoping from CachedTaskList (none). |
| `SyncMetadata` | ❌ No | ❌ **Global** | 🟠 High | Delta token key `TaskListsDeltaToken` is global. |
| `DistributedCacheEntry` | ❌ No | ❌ **Global** | ✅ Safe | MSAL token cache; keys are internally user-scoped by MSAL. |

---

## 3. Service Audit — Current User-Scoping Status

### 3.1 ApiKeyService — ✅ Properly Scoped

All three methods (`CreateKeyAsync`, `GetUserKeysAsync`, `RevokeKeyAsync`) accept an explicit `userId` parameter and filter all queries by it. `RevokeKeyAsync` validates key ownership before revoking. The `ApiKeys.razor` page extracts the OID from authentication claims and passes it to every call.

**No changes needed.**

### 3.2 TemplateService — 🔴 Not Scoped

| Method | Accepts userId? | Filters by user? |
|--------|:--------------:|:----------------:|
| `GetAllAsync()` | ❌ | ❌ Returns ALL templates |
| `GetByIdAsync(id)` | ❌ | ❌ Returns any template by ID |
| `CreateAsync(template)` | ❌ | ❌ No user association |
| `UpdateAsync(template)` | ❌ | ❌ No ownership check |
| `DeleteAsync(id)` | ❌ | ❌ No ownership check |
| `ExecuteTemplateAsync(id)` | ❌ | ❌ No ownership check |

**Callers affected:**
- `Templates.razor` — calls all CRUD methods without userId
- `Home.razor` — calls `GetAllAsync()` and `ExecuteTemplateAsync()` without userId
- `GET /api/templates` — returns all templates for any authenticated user
- `POST /api/templates/{id}/execute` — executes any template for any user
- `ChatService` — calls `GetAllAsync()` and `ExecuteTemplateAsync()` through AI tools

### 3.3 CachedTodoService — 🔴 Cache Not Scoped

The service wraps `GraphTodoService` (which is user-scoped via Graph API tokens) with a SQLite cache layer. The cache layer has no user isolation:

| Method | Data Source | User-Scoped? |
|--------|-----------|:------------:|
| `GetTaskListsAsync()` | Cache: `CachedTaskLists.Where(l => l.IsSynced)` | ❌ No user filter |
| `GetTasksAsync(listId)` | Cache: `CachedTasks.Where(t => t.ListId == listId)` | ❌ |
| `GetTodayTasksAsync()` | Cache: `CachedTasks.Where(t => !t.IsDeleted && t.DueDate == today)` | ❌ |
| `CreateTaskAsync(...)` | Graph API (user-scoped) + cache write | ⚠️ Write is correct, cache is shared |
| `UpdateTaskStatusAsync(...)` | Graph API (user-scoped) + cache write | ⚠️ Same |
| `SetTaskListSyncedAsync(...)` | Direct cache update | ❌ No ownership check |
| `GetNotSyncedTaskListsAsync()` | Cache | ❌ No user filter |

**Delta token scoping issues:**
- `TaskListsDeltaTokenKey = "TaskListsDeltaToken"` — single global SyncMetadata key shared by all users
- Per-list delta tokens stored in `CachedTaskList.DeltaToken` — per-list, but list has no UserId
- `ClearCacheAndInitialSyncAsync` deletes ALL rows from CachedTasks, CachedTaskLists, and SyncMetadata

**Cross-user contamination scenario:**
1. User A signs in → syncs → cache fills with User A's task lists and tasks
2. User B signs in → syncs → cache gets User B's lists and tasks added alongside User A's
3. User A queries `GetTodayTasksAsync()` → sees BOTH users' tasks
4. User B calls `ClearCacheAndInitialSyncAsync` → wipes User A's cache too

### 3.4 UserTimeZoneService — ✅ Properly Scoped

Extracts OID from claims (supports both OIDC and API key auth). Queries `Users` by OID. No changes needed.

### 3.5 UserPreferenceService — ✅ Properly Scoped

All methods accept explicit `userId`. No changes needed.

### 3.6 NotificationService — ✅ Safe (In-Memory, Scoped)

Registered as Scoped; each circuit/request gets its own instance. No persistence. No changes needed.

### 3.7 ChatService — ⚠️ Inherits Template Vulnerability

Calls `templateService.GetAllAsync()` and `templateService.ExecuteTemplateAsync()` without userId. Once `TemplateService` is fixed, `ChatService` must pass the current user's ID to template operations.

---

## 4. API Endpoint Audit

| Endpoint | Extracts userId? | Passes to service? | Scoped? |
|----------|:---------------:|:-----------------:|:-------:|
| `GET /api/templates` | ❌ | ❌ | 🔴 **No** |
| `POST /api/templates/{id}/execute` | ❌ | ❌ | 🔴 **No** |
| `GET /api/today` | Implicit (Graph) | Implicit | ✅ via Graph |
| `POST /api/tasks/{listId}/{taskId}/complete` | Implicit (Graph) | Implicit | ✅ via Graph |
| `GET /api/tasklists` | Implicit (Graph) | Implicit | ✅ via Graph |
| `GET /api/tasklists/{listId}/tasks` | Implicit (Graph) | Implicit | ✅ via Graph |

**Note:** Graph-backed endpoints are safe because the Graph API enforces per-user isolation at the token level. The template endpoints are the only ones that access local storage without user scoping.

---

## 5. Delta Token Validation Analysis

### Current State

Delta tokens are opaque strings returned by the Microsoft Graph delta API. They encode the point-in-time state of a resource for a specific user. A delta token obtained under User A's Graph credentials **cannot** be used with User B's credentials — the Graph API will reject it or return unexpected results.

**Current storage:**

| Token Type | Storage Location | Scoped? |
|-----------|-----------------|:-------:|
| Task lists delta | `SyncMetadata["TaskListsDeltaToken"]` | ❌ Global key |
| Per-list task delta | `CachedTaskList.DeltaToken` | ❌ Per-list but list has no UserId |

**Risk:** If User A syncs, their delta token is stored in `SyncMetadata["TaskListsDeltaToken"]`. If User B then syncs, this token is overwritten with User B's token. When User A syncs again, the stored token belongs to User B's Graph session — Graph will reject it, forcing a full re-sync (not a data leak, but causes performance degradation and potential data duplication in cache).

### Proposed Fix

Delta tokens must be keyed per-user:
- Task lists delta: `SyncMetadata[$"TaskListsDeltaToken:{userId}"]`
- Per-list task delta: Already stored per-list, but lists need a UserId column so they're user-isolated

---

## 6. Proposed Changes

### 6.1 Schema Changes (EF Core Migration)

**Add `UserId` to `TaskTemplate`:**
```csharp
public class TaskTemplate
{
    // ... existing properties ...
    public required string UserId { get; set; }
    public User? User { get; set; }
}
```

**Add `UserId` to `CachedTaskList`:**
```csharp
public class CachedTaskList
{
    // ... existing properties ...
    public required string UserId { get; set; }
}
```

**Add `UserId` to `CachedTask`:**
```csharp
public class CachedTask
{
    // ... existing properties ...
    public required string UserId { get; set; }
}
```

**Update `SyncMetadata`** — add UserId so delta tokens are per-user:
```csharp
public class SyncMetadata
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? UserId { get; set; }  // Nullable for backward compat
    public DateTime UpdatedUtc { get; set; }
}
```

**Indexes to add:**
- `CachedTaskList`: composite index on `{UserId, IsSynced}`
- `CachedTask`: composite index on `{UserId, IsDeleted, DueDate}`
- `TaskTemplate`: index on `UserId`
- `SyncMetadata`: composite key becomes `{Key, UserId}` or add index on `{Key, UserId}`

**AppDbContext OnModelCreating additions:**
- `TaskTemplate`: FK to User with `OnDelete: Cascade`
- `CachedTaskList`: index on `{UserId, IsSynced}`
- `CachedTask`: index on `{UserId, IsDeleted, DueDate}`
- Consider global query filter: `entity.HasQueryFilter(e => ...)` — but this requires `IHttpContextAccessor` injection into DbContext which adds complexity. Prefer explicit filtering in service layer.

### 6.2 ITemplateService Interface Changes

```csharp
public interface ITemplateService
{
    Task<IReadOnlyList<TaskTemplate>> GetAllAsync(string userId);
    Task<TaskTemplate?> GetByIdAsync(Guid id, string userId);
    Task<TaskTemplate> CreateAsync(TaskTemplate template, string userId);
    Task UpdateAsync(TaskTemplate template, string userId);
    Task DeleteAsync(Guid id, string userId);
    Task<TodoTask> ExecuteTemplateAsync(Guid templateId, string userId);
}
```

Every method requires explicit `userId`. Implementation must filter all queries by `UserId` and validate ownership on mutations.

### 6.3 CachedTodoService Changes

**User ID acquisition:** Add a private helper to extract user ID from the current authentication context (via `IHttpContextAccessor`).

**Per-user delta tokens:**
```csharp
private string GetTaskListsDeltaTokenKey(string userId) => $"TaskListsDeltaToken:{userId}";
```

**Per-user cache queries:** All `CachedTaskList` and `CachedTask` queries must include `.Where(e => e.UserId == userId)`.

**Per-user sync lock:** Replace static `SemaphoreSlim` with `ConcurrentDictionary<string, SemaphoreSlim>` keyed by userId. Each user syncs independently.

**Per-user cache clearing:** `ClearCacheAndInitialSyncAsync` must delete only the current user's rows:
```sql
DELETE FROM CachedTasks WHERE UserId = {userId}
DELETE FROM CachedTaskLists WHERE UserId = {userId}
DELETE FROM SyncMetadata WHERE Key = {deltaTokenKey}
```

### 6.4 API Endpoint Changes

All template endpoints must extract userId from claims and pass it to the service:

```csharp
api.MapGet("/templates", async (HttpContext context, ITemplateService templateService) =>
{
    var userId = GetUserId(context);
    var templates = await templateService.GetAllAsync(userId);
    return Results.Ok(templates);
});
```

Define a shared `GetUserId` helper for endpoints:
```csharp
static string GetUserId(HttpContext context) =>
    context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
    ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
    ?? throw new UnauthorizedAccessException("User ID not found in claims");
```

### 6.5 ChatService Changes

`ChatService` must obtain the current user's ID and pass it to all template service calls. The user ID should be captured when the service is constructed (scoped lifetime) or passed per-call.

### 6.6 Blazor Page Changes

- `Templates.razor`: Extract userId from `AuthenticationStateProvider` (same pattern as `ApiKeys.razor`) and pass to all `ITemplateService` calls.
- `Home.razor`: Same — extract userId and pass to `GetAllAsync()` and `ExecuteTemplateAsync()`.
- `SyncSettings.razor`: No changes needed if `CachedTodoService` internally resolves the current user.

---

## 7. Migration Strategy

### 7.1 Data Migration

Since this is a SQLite database and the app was previously single-user:

1. **Determine the existing user:** Query the `Users` table — if exactly one user exists, assign all orphaned rows to that user.
2. **TaskTemplate migration:**
   - Add `UserId` column (TEXT, nullable initially)
   - UPDATE all existing templates to set `UserId = (SELECT Id FROM Users LIMIT 1)`
   - ALTER column to NOT NULL
3. **CachedTaskList / CachedTask migration:**
   - Add `UserId` column (TEXT, nullable initially)
   - UPDATE to assign to the single user
   - ALTER to NOT NULL
4. **SyncMetadata migration:**
   - Rename key `TaskListsDeltaToken` → `TaskListsDeltaToken:{userId}` for the existing user

**SQLite limitation:** SQLite doesn't support `ALTER COLUMN` to add NOT NULL with a default. Use the table-rebuild pattern (as done for TaskTemplate Guid migration): create new table → copy data → drop old → rename.

### 7.2 Rollback Safety

- All changes are additive (new columns)
- Old data is preserved (just gets a UserId assigned)
- If rollback needed, the columns can be ignored by reverting code

### 7.3 Demo Mode

Demo mode uses a synthetic `demo-user` identity. All demo data should be scoped to a single demo user ID. `DemoGraphTodoClient` should continue to work unchanged since it doesn't use the cache layer.

---

## 8. Implementation Order

| Phase | Scope | Effort |
|-------|-------|--------|
| **Phase 1** | Add `UserId` to `TaskTemplate` + update `ITemplateService` + fix Blazor pages + fix API endpoints | Medium |
| **Phase 2** | Add `UserId` to `CachedTaskList` + `CachedTask` + update `CachedTodoService` + per-user delta tokens + per-user sync lock | Large |
| **Phase 3** | Update `ChatService` to pass userId to template operations | Small |
| **Phase 4** | (Optional) Add global query filters to `AppDbContext` for defense-in-depth | Small |

**Phase 1 is the security-critical path** — template data is directly user-managed and exposed via API. Phase 2 addresses the cache layer which is lower risk because the Graph API enforces isolation at the data source level (the cache just happens to mix users' data in the local store).

---

## 9. Summary of Gaps

| Gap | Severity | Affected Entities | Fix |
|-----|:--------:|-------------------|-----|
| Templates not user-scoped | 🔴 Critical | `TaskTemplate` | Add UserId, filter all queries |
| Cache not user-scoped | 🔴 Critical | `CachedTaskList`, `CachedTask` | Add UserId, filter all queries |
| Global delta token | 🟠 High | `SyncMetadata` | Key per-user: `TaskListsDeltaToken:{userId}` |
| Global sync lock | 🟠 High | `CachedTodoService` | Per-user `ConcurrentDictionary<string, SemaphoreSlim>` |
| Global cache clearing | 🟠 High | `CachedTodoService` | Filter DELETE by UserId |
| ChatService inherits gaps | 🟡 Medium | `ChatService` | Pass userId to template operations |
| No AppDbContext query filters | 🟡 Medium | All entities | Optional defense-in-depth |

---

## 10. Open Questions

1. **Should we add global query filters in AppDbContext?** Pro: defense-in-depth. Con: requires injecting user context into DbContext, adds complexity, can cause issues with migrations and admin queries.
2. **Should SyncMetadata key include UserId in the PK?** Currently PK is just `Key`. Making it a composite `{Key, UserId}` would be cleaner but requires table rebuild.
3. **Should per-list delta tokens on CachedTaskList just work via the new UserId column?** Since list IDs from Graph are already user-unique, per-list tokens are implicitly per-user. Adding UserId to CachedTaskList makes this explicit and prevents any edge case.
