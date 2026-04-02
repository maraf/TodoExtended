using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using TodoExtended.Web.Data;
using System.Globalization;

namespace TodoExtended.Web.Services;

public class CachedTodoService(
    GraphTodoService graphService,
    IGraphTodoClient graphClient,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<TodoCacheOptions> options,
    IUserTimeZoneService userTimeZoneService,
    ILogger<CachedTodoService> logger) : ITodoService
{
    private readonly TodoCacheOptions _options = options.Value;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _syncLocks = new();

    private static string GetTaskListsDeltaTokenKey(string userId) => $"TaskListsDeltaToken:{userId}";

    private static SemaphoreSlim GetSyncLock(string userId) =>
        _syncLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

    public async Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureListsCacheValidAsync(db, userId);
        
        return await db.CachedTaskLists
            .Where(l => l.UserId == userId && l.IsSynced)
            .OrderBy(l => l.DisplayName)
            .Select(l => new TodoTaskList(l.Id, l.DisplayName, l.IsSynced))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TodoTaskList>> GetNotSyncedTaskListsAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.CachedTaskLists
            .Where(l => l.UserId == userId && !l.IsSynced)
            .OrderBy(l => l.DisplayName)
            .Select(l => new TodoTaskList(l.Id, l.DisplayName, l.IsSynced))
            .ToListAsync();
    }

    public async Task SetTaskListSyncedAsync(string taskListId, bool isSynced, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var cachedList = await db.CachedTaskLists
            .FirstOrDefaultAsync(l => l.Id == taskListId && l.UserId == userId)
            ?? throw new InvalidOperationException($"Task list '{taskListId}' not found in cache.");

        cachedList.IsSynced = isSynced;
        cachedList.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Task list {ListId} isSynced={IsSynced}", taskListId, isSynced);
    }

    public async Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureListCacheValidAsync(db, taskListId, userId);
        
        var tasks = await db.CachedTasks
            .Where(t => t.ListId == taskListId && t.UserId == userId && !t.IsDeleted)
            .ToListAsync();
        
        return tasks
            .Select(t => new TodoTask(
                t.Id, t.Title, t.Body, t.IsCompleted, t.DueDate, t.Importance, t.HasReminder, t.IsRecurring))
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => ImportanceSortOrder(t.Importance))
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<TodoTask?> GetTaskAsync(string taskListId, string taskId, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureListCacheValidAsync(db, taskListId, userId);

        var t = await db.CachedTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ListId == taskListId && t.UserId == userId && !t.IsDeleted);

        return t == null ? null : new TodoTask(t.Id, t.Title, t.Body, t.IsCompleted, t.DueDate, t.Importance, t.HasReminder, t.IsRecurring);
    }

    public async Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync(string userId)
    {
        var today = await userTimeZoneService.GetTodayAsync();
        return await GetTasksForDateAsync(userId, today);
    }

    public async Task<IReadOnlyList<TodoTaskWithList>> GetTomorrowTasksAsync(string userId)
    {
        var today = await userTimeZoneService.GetTodayAsync();
        return await GetTasksForDateAsync(userId, today.AddDays(1));
    }

    private async Task<IReadOnlyList<TodoTaskWithList>> GetTasksForDateAsync(string userId, DateOnly date)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureCacheValidAsync(db, userId);

        var tasks = await db.CachedTasks
            .Include(t => t.List)
            .Where(t => t.UserId == userId && !t.IsDeleted && t.DueDate == date && t.List!.IsSynced)
            .ToListAsync();

        return tasks
            .Select(t => new TodoTaskWithList(
                t.Id, t.Title, t.Body, t.IsCompleted, t.DueDate, t.Importance,
                t.ListId, t.List!.DisplayName, t.HasReminder, t.IsRecurring))
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => ImportanceSortOrder(t.Importance))
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private const int MaxSearchQueryLength = 500;

    public async Task<IReadOnlyList<TodoTaskWithList>> SearchTasksAsync(string query, string userId)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > MaxSearchQueryLength)
            return [];

        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureCacheValidAsync(db, userId);

        var likePattern = $"%{EscapeLikePattern(query)}%";
        var matchingTasks = await db.CachedTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId && !t.IsDeleted && t.List!.IsSynced
                && EF.Functions.Like(t.Title, likePattern, "\\"))
            .Select(t => new TodoTaskWithList(
                t.Id, t.Title, t.Body, t.IsCompleted, t.DueDate, t.Importance,
                t.ListId, t.List!.DisplayName, t.HasReminder, t.IsRecurring))
            .ToListAsync();

        return matchingTasks
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => ImportanceSortOrder(t.Importance))
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTaskList>> SearchTaskListsAsync(string query, string userId)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > MaxSearchQueryLength)
            return [];

        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureListsCacheValidAsync(db, userId);

        var likePattern = $"%{EscapeLikePattern(query)}%";
        var matchingLists = await db.CachedTaskLists
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.IsSynced
                && EF.Functions.Like(l.DisplayName, likePattern, "\\"))
            .Select(l => new TodoTaskList(l.Id, l.DisplayName, l.IsSynced))
            .ToListAsync();

        return matchingLists
            .OrderBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string EscapeLikePattern(string query) =>
        query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")
             .Replace("[", "\\[").Replace("]", "\\]");

    public async Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate, string userId, TimeOnly? reminderTime = null)
    {
        var created = await graphService.CreateTaskAsync(taskListId, title, dueDate, userId, reminderTime);
        
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var cachedTask = new CachedTask
        {
            Id = created.Id,
            ListId = taskListId,
            Title = created.Title,
            Body = created.Body,
            IsCompleted = created.IsCompleted,
            DueDate = created.DueDate,
            Importance = created.Importance,
            HasReminder = created.HasReminder,
            IsRecurring = created.IsRecurring,
            IsDeleted = false,
            LastSyncUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            UserId = userId,
        };
        
        db.CachedTasks.Add(cachedTask);
        await db.SaveChangesAsync();
        
        return created;
    }

    public async Task UpdateTaskStatusAsync(string taskListId, string taskId, bool completed, string userId)
    {
        await graphService.UpdateTaskStatusAsync(taskListId, taskId, completed, userId);
        
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var cachedTask = await db.CachedTasks.FindAsync(taskId);
        if (cachedTask != null)
        {
            cachedTask.IsCompleted = completed;
            cachedTask.UpdatedUtc = DateTime.UtcNow;
            cachedTask.LastSyncUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task SetTaskDueDateAsync(string taskListId, string taskId, DateOnly dueDate, string userId)
    {
        await graphService.SetTaskDueDateAsync(taskListId, taskId, dueDate, userId);

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var cachedTask = await db.CachedTasks.FindAsync(taskId);
        if (cachedTask != null)
        {
            cachedTask.DueDate = dueDate;
            cachedTask.UpdatedUtc = DateTime.UtcNow;
            cachedTask.LastSyncUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task SetTaskReminderAsync(string taskListId, string taskId, DateOnly reminderDate, TimeOnly reminderTime, string userId)
    {
        await graphService.SetTaskReminderAsync(taskListId, taskId, reminderDate, reminderTime, userId);

        // Reminder state is not stored in the local cache, so mark the list as stale
        // so the next read will re-sync from Graph and reflect the updated reminder.
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var cachedList = await db.CachedTaskLists.FirstOrDefaultAsync(l => l.Id == taskListId && l.UserId == userId);
        if (cachedList != null)
        {
            cachedList.LastSyncUtc = DateTime.MinValue;
            await db.SaveChangesAsync();
        }
    }

    private async Task EnsureCacheValidAsync(AppDbContext db, string userId)
    {
        if (!await IsCacheStaleAsync(db, userId))
            return;

        var syncLock = GetSyncLock(userId);
        await syncLock.WaitAsync();
        try
        {
            if (!await IsCacheStaleAsync(db, userId))
                return;

            await SyncAsync(db, userId);
        }
        catch (MsalServiceException)
        {
            logger.LogWarning("Cache sync aborted: irrecoverable MSAL authentication failure (e.g. invalid_client). Re-throwing to trigger sign-out");
            throw;
        }
        catch (ObjectDisposedException)
        {
            logger.LogWarning("Cache sync aborted: authentication scope disposed (Blazor circuit likely disconnected). Serving stale cache");
        }
        finally
        {
            syncLock.Release();
        }
    }

    private async Task EnsureListsCacheValidAsync(AppDbContext db, string userId)
    {
        if (!await IsCacheStaleAsync(db, userId))
            return;

        var syncLock = GetSyncLock(userId);
        await syncLock.WaitAsync();
        try
        {
            if (!await IsCacheStaleAsync(db, userId))
                return;

            await SyncListsOnlyAsync(db, userId);
        }
        catch (MsalServiceException)
        {
            logger.LogWarning("Lists-only cache sync aborted: irrecoverable MSAL authentication failure. Re-throwing to trigger sign-out");
            throw;
        }
        catch (ObjectDisposedException)
        {
            logger.LogWarning("Lists-only cache sync aborted: authentication scope disposed (Blazor circuit likely disconnected). Serving stale cache");
        }
        finally
        {
            syncLock.Release();
        }
    }

    private async Task EnsureListCacheValidAsync(AppDbContext db, string taskListId, string userId)
    {
        if (!await IsListCacheStaleAsync(db, taskListId))
            return;

        var syncLock = GetSyncLock(userId);
        await syncLock.WaitAsync();
        try
        {
            if (!await IsListCacheStaleAsync(db, taskListId))
                return;

            var list = await db.CachedTaskLists.FirstOrDefaultAsync(l => l.Id == taskListId && l.UserId == userId);
            if (list == null)
            {
                logger.LogInformation("List {ListId} not in cache, performing full sync", taskListId);
                await SyncAsync(db, userId);
                return;
            }

            await SyncTasksForListAsync(db, list.Id, list.DeltaToken, userId);
            list.LastSyncUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (MsalServiceException)
        {
            logger.LogWarning("Single-list sync for {ListId} aborted: irrecoverable MSAL authentication failure. Re-throwing to trigger sign-out", taskListId);
            throw;
        }
        catch (ObjectDisposedException)
        {
            logger.LogWarning("Single-list sync for {ListId} aborted: authentication scope disposed (Blazor circuit likely disconnected). Serving stale cache", taskListId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Single-list sync failed for {ListId}, falling back to full sync", taskListId);
            if (ShouldRebuildCache(ex))
                await ClearCacheAndInitialSyncAsync(db, userId);
            else
                throw;
        }
        finally
        {
            syncLock.Release();
        }
    }

    private async Task<bool> IsListCacheStaleAsync(AppDbContext db, string taskListId)
    {
        var cacheMaxAge = TimeSpan.FromMinutes(_options.StalenessThresholdMinutes);
        var lastSync = await db.CachedTaskLists
            .Where(l => l.Id == taskListId)
            .Select(l => (DateTime?)l.LastSyncUtc)
            .FirstOrDefaultAsync();

        if (lastSync == null)
            return true;

        return (DateTime.UtcNow - lastSync.Value) > cacheMaxAge;
    }

    private async Task<bool> IsCacheStaleAsync(AppDbContext db, string userId)
    {
        var cacheMaxAge = TimeSpan.FromMinutes(_options.StalenessThresholdMinutes);
        var now = DateTime.UtcNow;
        
        var oldestSync = await db.CachedTaskLists
            .Where(l => l.UserId == userId && l.IsSynced)
            .Select(l => (DateTime?)l.LastSyncUtc)
            .MinAsync();
        
        if (oldestSync == null)
        {
            logger.LogDebug("Cache is stale: no synced lists in cache for user {UserId}", userId);
            return true;
        }

        var isStale = (now - oldestSync.Value) > cacheMaxAge;
        if (isStale)
        {
            logger.LogDebug("Cache is stale: oldest sync {OldestSync}, age {Age} > threshold {Threshold}",
                oldestSync, now - oldestSync.Value, cacheMaxAge);
        }
        
        return isStale;
    }

    private async Task SyncAsync(AppDbContext db, string userId)
    {
        logger.LogInformation("Starting cache sync for user {UserId}", userId);
        
        try
        {
            var hasAnyLists = await db.CachedTaskLists.Where(l => l.UserId == userId).AnyAsync();
            
            if (!hasAnyLists)
            {
                logger.LogInformation("Cold cache: performing initial sync");
                await InitialSyncAsync(db, userId);
            }
            else
            {
                logger.LogInformation("Warm cache: performing delta sync");
                await DeltaSyncAsync(db, userId);
            }
            
            logger.LogInformation("Cache sync completed successfully");
        }
        catch (MsalServiceException)
        {
            logger.LogWarning("Cache sync aborted: irrecoverable MSAL authentication failure (e.g. invalid_client)");
            throw;
        }
        catch (ObjectDisposedException)
        {
            logger.LogWarning("Cache sync aborted: authentication scope disposed (Blazor circuit likely disconnected)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cache sync failed");
            
            if (ShouldRebuildCache(ex))
            {
                logger.LogWarning("Rebuilding cache due to invalid delta token or sync error");
                await ClearCacheAndInitialSyncAsync(db, userId);
            }
            else
            {
                throw;
            }
        }
    }

    private async Task SyncListsOnlyAsync(AppDbContext db, string userId)
    {
        logger.LogInformation("Starting lists-only cache sync for user {UserId}", userId);

        try
        {
            var hasAnyLists = await db.CachedTaskLists.Where(l => l.UserId == userId).AnyAsync();

            if (!hasAnyLists)
            {
                var lists = await graphService.GetTaskListsAsync(userId);
                var now = DateTime.UtcNow;

                foreach (var list in lists)
                {
                    db.CachedTaskLists.Add(new CachedTaskList
                    {
                        Id = list.Id,
                        DisplayName = list.DisplayName,
                        IsSynced = true,
                        DeltaToken = null,
                        LastSyncUtc = now,
                        CreatedUtc = now,
                        UpdatedUtc = now,
                        UserId = userId,
                    });
                }

                await db.SaveChangesAsync();
            }
            else
            {
                await SyncTaskListsAsync(db, userId);

                // Update LastSyncUtc so staleness check passes without syncing tasks
                var lists = await db.CachedTaskLists
                    .Where(l => l.UserId == userId && l.IsSynced)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                foreach (var list in lists)
                    list.LastSyncUtc = now;

                await db.SaveChangesAsync();
            }

            logger.LogInformation("Lists-only cache sync completed successfully");
        }
        catch (MsalServiceException)
        {
            logger.LogWarning("Lists-only cache sync aborted: irrecoverable MSAL authentication failure");
            throw;
        }
        catch (ObjectDisposedException)
        {
            logger.LogWarning("Lists-only cache sync aborted: authentication scope disposed (Blazor circuit likely disconnected)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lists-only cache sync failed");

            if (ShouldRebuildCache(ex))
            {
                logger.LogWarning("Falling back to full sync due to error");
                await ClearCacheAndInitialSyncAsync(db, userId);
            }
            else
            {
                throw;
            }
        }
    }

    private async Task InitialSyncAsync(AppDbContext db, string userId)
    {
        var lists = await graphService.GetTaskListsAsync(userId);
        var now = DateTime.UtcNow;

        foreach (var list in lists)
        {
            var cachedList = new CachedTaskList
            {
                Id = list.Id,
                DisplayName = list.DisplayName,
                IsSynced = true,
                DeltaToken = null,
                LastSyncUtc = now,
                CreatedUtc = now,
                UpdatedUtc = now,
                UserId = userId,
            };
            
            db.CachedTaskLists.Add(cachedList);
            await db.SaveChangesAsync();
        }

        // Sync tasks for all lists in parallel
        await SyncTasksForListsBatchAsync(
            lists.Select(l => (l.Id, (string?)null)).ToList(), userId);
    }

    private async Task DeltaSyncAsync(AppDbContext db, string userId)
    {
        await SyncTaskListsAsync(db, userId);

        // Delta sync only processes changed tasks, so if the tags table is empty while
        // the task cache is warm (e.g. after a migration that cleared tags), unchanged
        // tasks would never have their tags extracted. Rebuild tags from cached tasks now.
        // Avoid doing an O(N) scan for users who never use tags: only rebuild if there
        // exists at least one non-deleted cached task whose title contains a '#'.
        var hasAnyTags = await db.CachedTags.AnyAsync(t => t.UserId == userId);
        if (!hasAnyTags)
        {
            var hasPotentialTagTasks = await db.CachedTasks
                .AnyAsync(t => t.UserId == userId && !t.IsDeleted && t.Title.Contains("#"));

            if (hasPotentialTagTasks)
                await RebuildTagsFromCachedTasksAsync(db, userId);
        }

        var lists = await db.CachedTaskLists
            .Where(l => l.UserId == userId && l.IsSynced)
            .Select(l => new { l.Id, l.DeltaToken })
            .ToListAsync();

        foreach (var list in lists)
        {
            if (list.DeltaToken == null)
                logger.LogWarning("List {ListId} has no delta token: a full sync will be performed and deleted tasks will not be detected", list.Id);
        }

        await SyncTasksForListsBatchAsync(
            lists.Select(l => (l.Id, l.DeltaToken)).ToList(), userId);
    }

    private async Task SyncTasksForListsBatchAsync(List<(string Id, string? DeltaToken)> lists, string userId)
    {
        if (lists.Count == 0) return;

        logger.LogInformation("Batch-fetching task deltas for {Count} lists", lists.Count);

        // Phase 1: Batch-fetch first delta pages (N lists → ceil(N/20) HTTP calls instead of N)
        var requests = lists.Select(l => (l.Id, l.DeltaToken)).ToList();
        var batchResults = await graphClient.GetTasksDeltaBatchAsync(requests);

        // Phase 2: Process results in parallel (pagination follows individually per list)
        using var throttle = new SemaphoreSlim(_options.MaxParallelListSync);
        var tasks = batchResults.Select(async kvp =>
        {
            await throttle.WaitAsync();
            try
            {
                await using var scopedDb = await dbContextFactory.CreateDbContextAsync();
                await ProcessTasksDeltaPagesAsync(scopedDb, kvp.Key, kvp.Value, userId);
            }
            catch (MsalServiceException)
            {
                logger.LogWarning("Task sync for list {ListId} aborted during batch: irrecoverable MSAL authentication failure", kvp.Key);
            }
            catch (ObjectDisposedException)
            {
                logger.LogWarning("Task sync for list {ListId} aborted during batch: authentication scope disposed (Blazor circuit likely disconnected)", kvp.Key);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task SyncTaskListsAsync(AppDbContext db, string userId)
    {
        logger.LogDebug("Syncing task lists with delta query");

        var deltaTokenKey = GetTaskListsDeltaTokenKey(userId);
        var deltaTokenMetadata = await db.SyncMetadata.FindAsync(deltaTokenKey);
        var deltaToken = deltaTokenMetadata?.Value;

        try
        {
            var page = await graphClient.GetListsDeltaPageAsync(deltaToken);

            while (true)
            {
                if (page.Value.Count > 0)
                {
                    foreach (var graphList in page.Value)
                    {
                        if (graphList.AdditionalData?.ContainsKey("@removed") == true)
                        {
                            var cachedList = await db.CachedTaskLists.FindAsync(graphList.Id);
                            if (cachedList != null)
                            {
                                logger.LogDebug("Removing task list {ListId} from cache", graphList.Id);
                                db.CachedTaskLists.Remove(cachedList);
                            }
                            else
                            {
                                logger.LogDebug("Skipping removal of task list {ListId}: not found in cache", graphList.Id);
                            }
                        }
                        else
                        {
                            var cachedList = await db.CachedTaskLists.FindAsync(graphList.Id);
                            var now = DateTime.UtcNow;

                            if (cachedList == null)
                            {
                                logger.LogDebug("Adding new task list {ListId} to cache", graphList.Id);
                                cachedList = new CachedTaskList
                                {
                                    Id = graphList.Id!,
                                    DisplayName = graphList.DisplayName ?? "Untitled",
                                    IsSynced = true,
                                    DeltaToken = null,
                                    LastSyncUtc = now,
                                    CreatedUtc = now,
                                    UpdatedUtc = now,
                                    UserId = userId,
                                };
                                db.CachedTaskLists.Add(cachedList);
                            }
                            else
                            {
                                logger.LogDebug("Updating task list {ListId} in cache", graphList.Id);
                                cachedList.DisplayName = graphList.DisplayName ?? "Untitled";
                                cachedList.UpdatedUtc = now;
                            }
                        }
                    }

                    await db.SaveChangesAsync();
                }

                if (!string.IsNullOrEmpty(page.OdataNextLink))
                {
                    logger.LogDebug("Fetching next page of task lists delta");
                    page = await graphClient.GetListsDeltaPageAsync(page.OdataNextLink);
                }
                else
                {
                    if (!string.IsNullOrEmpty(page.OdataDeltaLink))
                    {
                        logger.LogDebug("Storing task lists delta token");
                        if (deltaTokenMetadata == null)
                        {
                            deltaTokenMetadata = new SyncMetadata
                            {
                                Key = deltaTokenKey,
                                Value = page.OdataDeltaLink,
                                UpdatedUtc = DateTime.UtcNow,
                                UserId = userId,
                            };
                            db.SyncMetadata.Add(deltaTokenMetadata);
                        }
                        else
                        {
                            deltaTokenMetadata.Value = page.OdataDeltaLink;
                            deltaTokenMetadata.UpdatedUtc = DateTime.UtcNow;
                        }
                        await db.SaveChangesAsync();
                    }
                    else
                    {
                        logger.LogWarning("No delta link returned for task lists: task lists delta token will not be refreshed, next sync will perform a full task lists sync");
                    }
                    break;
                }
            }
        }
        catch (MsalServiceException)
        {
            logger.LogWarning("Task lists delta sync aborted: irrecoverable MSAL authentication failure");
            throw;
        }
        catch (ObjectDisposedException)
        {
            logger.LogWarning("Task lists delta sync aborted: authentication scope disposed (Blazor circuit likely disconnected)");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing task lists delta");
            throw;
        }
    }

    private async Task SyncTasksForListAsync(AppDbContext scopedDb, string listId, string? deltaToken, string userId)
    {
        if (string.IsNullOrEmpty(deltaToken))
            logger.LogWarning("Syncing tasks for list {ListId}: no delta token available, performing full sync — @removed items are not returned by a full sync so deleted tasks will not be detected", listId);
        else
            logger.LogDebug("Syncing tasks for list {ListId} with delta token", listId);

        try
        {
            var firstPage = await graphClient.GetTasksDeltaPageAsync(listId, deltaToken);
            await ProcessTasksDeltaPagesAsync(scopedDb, listId, firstPage, userId);
        }
        catch (MsalServiceException)
        {
            logger.LogWarning("Task sync for list {ListId} aborted: irrecoverable MSAL authentication failure", listId);
            throw;
        }
        catch (ObjectDisposedException)
        {
            logger.LogWarning("Task sync for list {ListId} aborted: authentication scope disposed (Blazor circuit likely disconnected)", listId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing tasks for list {ListId}", listId);
            throw;
        }
    }

    /// <summary>
    /// Processes task delta pages starting from a provided first page. Follows pagination
    /// (OdataNextLink) until all pages are consumed, then saves the delta token.
    /// Shared by both single-list sync and batch sync paths.
    /// </summary>
    private async Task ProcessTasksDeltaPagesAsync(AppDbContext scopedDb, string listId, GraphDeltaPage<Microsoft.Graph.Models.TodoTask> page, string userId)
    {
        // Pre-load all tag names currently pinned by this user to avoid per-tag DB lookups in the loop
        var pinnedTagNames = await scopedDb.CachedTags
            .Where(t => t.UserId == userId && t.IsPinned)
            .Select(t => t.Name)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            if (page.Value.Count > 0)
            {
                logger.LogDebug("Delta page for list {ListId}: {Count} items", listId, page.Value.Count);
                foreach (var graphTask in page.Value)
                {
                    if (graphTask.AdditionalData?.ContainsKey("@removed") == true)
                    {
                        if (graphTask.Id is null)
                        {
                            logger.LogWarning("Received @removed task with null Id in list {ListId}; skipping", listId);
                            continue;
                        }
                        logger.LogDebug("Task {TaskId} detected as @removed in list {ListId}", graphTask.Id, listId);
                        var cachedTask = await scopedDb.CachedTasks.FindAsync(graphTask.Id);
                        if (cachedTask != null)
                        {
                            logger.LogDebug("Soft deleting task {TaskId} from cache (was IsDeleted={WasDeleted})", graphTask.Id, cachedTask.IsDeleted);
                            cachedTask.IsDeleted = true;
                            cachedTask.UpdatedUtc = DateTime.UtcNow;
                        }
                        else
                        {
                            logger.LogDebug("Skipping soft-delete for task {TaskId}: not found in cache", graphTask.Id);
                        }
                    }
                    else
                    {
                        var cachedTask = await scopedDb.CachedTasks.FindAsync(graphTask.Id);
                        var now = DateTime.UtcNow;
                        var dueDate = ParseDueDate(graphTask.DueDateTime);
                        var title = graphTask.Title ?? "Untitled";
                        var hasReminder = graphTask.IsReminderOn == true;
                        var isRecurring = graphTask.Recurrence != null;

                        if (cachedTask == null)
                        {
                            logger.LogDebug("Adding new task {TaskId} to cache", graphTask.Id);
                            cachedTask = new CachedTask
                            {
                                Id = graphTask.Id!,
                                ListId = listId,
                                Title = title,
                                Body = graphTask.Body?.Content,
                                IsCompleted = graphTask.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                                DueDate = dueDate,
                                Importance = graphTask.Importance?.ToString(),
                                HasReminder = hasReminder,
                                IsRecurring = isRecurring,
                                IsDeleted = false,
                                LastSyncUtc = now,
                                CreatedUtc = now,
                                UpdatedUtc = now,
                                UserId = userId,
                            };
                            scopedDb.CachedTasks.Add(cachedTask);
                            // Add tags to the same context — EF Core will insert the task before the join rows
                            await AddTagsToContextAsync(scopedDb, cachedTask, TagExtractor.ExtractTags(title), userId, pinnedTagNames);
                        }
                        else
                        {
                            logger.LogDebug("Updating task {TaskId} in cache", graphTask.Id);
                            cachedTask.Title = title;
                            cachedTask.Body = graphTask.Body?.Content;
                            cachedTask.IsCompleted = graphTask.Status == Microsoft.Graph.Models.TaskStatus.Completed;
                            cachedTask.DueDate = dueDate;
                            cachedTask.Importance = graphTask.Importance?.ToString();
                            cachedTask.HasReminder = hasReminder;
                            cachedTask.IsRecurring = isRecurring;
                            cachedTask.IsDeleted = false;
                            cachedTask.UpdatedUtc = now;
                            cachedTask.LastSyncUtc = now;
                            await UpdateTagsForExistingTaskAsync(scopedDb, cachedTask, title, userId, pinnedTagNames);
                        }
                    }
                }

                await scopedDb.SaveChangesAsync();
            }

            if (!string.IsNullOrEmpty(page.OdataNextLink))
            {
                logger.LogDebug("Fetching next page of tasks delta for list {ListId}", listId);
                page = await graphClient.GetTasksDeltaPageAsync(listId, page.OdataNextLink);
            }
            else
            {
                if (!string.IsNullOrEmpty(page.OdataDeltaLink))
                {
                    logger.LogDebug("Storing delta token for list {ListId}", listId);
                    var cachedList = await scopedDb.CachedTaskLists.FindAsync(listId);
                    if (cachedList != null)
                    {
                        cachedList.DeltaToken = page.OdataDeltaLink;
                        cachedList.LastSyncUtc = DateTime.UtcNow;
                        await scopedDb.SaveChangesAsync();
                    }
                    else
                    {
                        logger.LogWarning("Cannot store delta token for list {ListId}: list not found in cache. Future syncs will perform a full sync for this list until it is re-added to the cache, meaning task deletions will not be detected during that time", listId);
                    }
                }
                else
                {
                    logger.LogWarning("No delta link returned for list {ListId}: delta token will not be refreshed, next sync will perform a full sync and deleted tasks will not be detected", listId);
                }
                break;
            }
        }
    }

    private async Task ClearCacheAndInitialSyncAsync(AppDbContext db, string userId)
    {
        logger.LogInformation("Clearing cache for user {UserId} and performing full rebuild", userId);
        var deltaTokenKey = GetTaskListsDeltaTokenKey(userId);
        
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CachedTasks WHERE UserId = {0}", userId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CachedTaskLists WHERE UserId = {0}", userId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CachedTags WHERE UserId = {0}", userId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM SyncMetadata WHERE Key = {0}", deltaTokenKey);
        
        await InitialSyncAsync(db, userId);
    }

    /// <summary>
    /// Rebuilds <see cref="CachedTag"/> rows and their M:N task associations by extracting
    /// tags from the titles of all non-deleted cached tasks for the user.
    /// Called at the start of a delta sync when the tags table is unexpectedly empty
    /// (e.g. after a migration or partial data loss) but the task cache is still warm.
    /// </summary>
    private async Task RebuildTagsFromCachedTasksAsync(AppDbContext db, string userId)
    {
        var cachedTasks = await db.CachedTasks
            .Where(t => t.UserId == userId && !t.IsDeleted)
            .ToListAsync();

        if (cachedTasks.Count == 0)
            return;

        logger.LogInformation("Tags DB is empty for user {UserId}; rebuilding from {Count} cached tasks", userId, cachedTasks.Count);

        // Pass 1: collect all distinct tag names and map each task to its tags
        var taskTagNames = new Dictionary<CachedTask, List<string>>();
        var allTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cachedTask in cachedTasks)
        {
            var tagNames = TagExtractor.ExtractTags(cachedTask.Title);
            if (tagNames.Count == 0) continue;

            taskTagNames[cachedTask] = [.. tagNames];
            foreach (var tagName in tagNames)
                allTagNames.Add(tagName);
        }

        if (allTagNames.Count == 0)
            return;

        // Pass 2: fetch any already-existing CachedTag rows in a single query
        var existingTags = await db.CachedTags
            .Where(t => t.UserId == userId && allTagNames.Contains(t.Name))
            .ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

        // Pass 3: create missing CachedTag aggregates once (no per-task DB round-trips)
        foreach (var tagName in allTagNames)
        {
            if (existingTags.ContainsKey(tagName)) continue;

            var newTag = new CachedTag { Name = tagName, UserId = userId };
            db.CachedTags.Add(newTag);
            existingTags[tagName] = newTag;
        }

        // Pass 4: link tags to tasks in-memory (EF Core writes join rows on SaveChanges)
        foreach (var (cachedTask, tagNamesForTask) in taskTagNames)
        {
            foreach (var tagName in tagNamesForTask)
            {
                if (existingTags.TryGetValue(tagName, out var tag))
                    cachedTask.Tags.Add(tag);
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Tags rebuilt from cached tasks for user {UserId}", userId);
    }

    private static bool ShouldRebuildCache(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("410") || 
               message.Contains("Gone") || 
               message.Contains("delta") && message.Contains("invalid");
    }

    /// <summary>
    /// Finds or creates <see cref="CachedTag"/> aggregate rows and links them to a brand-new
    /// task via the EF Core native M:N relationship. EF Core inserts the join rows automatically
    /// on the next <c>SaveChangesAsync</c>. Existing <see cref="CachedTag"/> rows are fetched in
    /// a single batch query to avoid per-tag <c>FindAsync</c> DB round-trips.
    /// </summary>
    private static async Task AddTagsToContextAsync(
        AppDbContext db, CachedTask task, IReadOnlyList<string> tags, string userId,
        HashSet<string> pinnedTagNames)
    {
        if (tags.Count == 0) return;

        // Prefetch all existing CachedTag rows for these names in a single query
        var existingInDb = await db.CachedTags
            .Where(t => t.UserId == userId && tags.Contains(t.Name))
            .ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var tagName in tags)
        {
            // Prefer already-tracked instance (from earlier in the same sync page)
            var tag = db.CachedTags.Local.FirstOrDefault(t => t.Name == tagName && t.UserId == userId);
            if (tag == null && !existingInDb.TryGetValue(tagName, out tag))
            {
                tag = new CachedTag
                {
                    Name = tagName,
                    UserId = userId,
                    IsPinned = pinnedTagNames.Contains(tagName),
                };
                db.CachedTags.Add(tag);
            }

            task.Tags.Add(tag);
        }
    }

    /// <summary>
    /// Reconciles the <see cref="CachedTag"/> M:N collection for a task whose title has changed.
    /// Removed tag links are deleted; new <see cref="CachedTag"/> aggregates are upserted and
    /// linked via the native M:N relationship. New tag rows are fetched in a single batch query
    /// to avoid per-tag <c>FindAsync</c> DB round-trips.
    /// </summary>
    private async Task UpdateTagsForExistingTaskAsync(
        AppDbContext db, CachedTask task, string title, string userId,
        HashSet<string> pinnedTagNames)
    {
        var extractedTags = TagExtractor.ExtractTags(title);
        var extractedTagSet = extractedTags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Eagerly load the current tag collection for this task
        await db.Entry(task).Collection(t => t.Tags).LoadAsync();

        // Unlink tags that are no longer present in the title
        var tagsToRemove = task.Tags.Where(t => !extractedTagSet.Contains(t.Name)).ToList();
        foreach (var tag in tagsToRemove)
            task.Tags.Remove(tag);

        var existingTagNames = task.Tags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Only process tags that aren't already linked
        var newTagNames = extractedTagSet.Where(n => !existingTagNames.Contains(n)).ToList();
        if (newTagNames.Count == 0) return;

        // Prefetch all existing CachedTag rows for the new names in a single query
        var existingInDb = await db.CachedTags
            .Where(t => t.UserId == userId && newTagNames.Contains(t.Name))
            .ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var tagName in newTagNames)
        {
            var tag = db.CachedTags.Local.FirstOrDefault(t => t.Name == tagName && t.UserId == userId);
            if (tag == null && !existingInDb.TryGetValue(tagName, out tag))
            {
                tag = new CachedTag
                {
                    Name = tagName,
                    UserId = userId,
                    IsPinned = pinnedTagNames.Contains(tagName),
                };
                db.CachedTags.Add(tag);
            }

            task.Tags.Add(tag);
        }
    }

    /// <summary>
    /// Converts Graph's dateTimeTimeZone to a DateOnly.
    /// Microsoft To Do stores due dates as midnight-local-time converted to UTC
    /// (e.g., March 6 00:00 CET → 2026-03-05T23:00:00 UTC). Since the original
    /// value is always midnight in some timezone, adding 12 hours and taking the
    /// date gives the correct result for all practical timezones (UTC-12 to UTC+12).
    /// </summary>
    private DateOnly? ParseDueDate(Microsoft.Graph.Models.DateTimeTimeZone? dueDateTime)
    {
        if (dueDateTime?.DateTime is null) return null;

        logger.LogDebug("ParseDueDate: raw='{DateTime}' timeZone='{TimeZone}'", dueDateTime.DateTime, dueDateTime.TimeZone);

        var dt = DateTime.Parse(dueDateTime.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None);
        var result = DateOnly.FromDateTime(dt.AddHours(12));
        logger.LogDebug("ParseDueDate: result={Result}", result);
        return result;
    }

    private static int ImportanceSortOrder(string? importance) => importance?.ToLowerInvariant() switch
    {
        "high" => 0,
        "normal" => 1,
        "low" => 2,
        _ => 1,
    };
}
