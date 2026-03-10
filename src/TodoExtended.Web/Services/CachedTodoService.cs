using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    private static readonly SemaphoreSlim _syncLock = new(1, 1);
    private static readonly string TaskListsDeltaTokenKey = "TaskListsDeltaToken";

    public async Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureListsCacheValidAsync(db);
        
        return await db.CachedTaskLists
            .Where(l => l.IsSynced)
            .OrderBy(l => l.DisplayName)
            .Select(l => new TodoTaskList(l.Id, l.DisplayName, l.IsSynced))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TodoTaskList>> GetNotSyncedTaskListsAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.CachedTaskLists
            .Where(l => !l.IsSynced)
            .OrderBy(l => l.DisplayName)
            .Select(l => new TodoTaskList(l.Id, l.DisplayName, l.IsSynced))
            .ToListAsync();
    }

    public async Task SetTaskListSyncedAsync(string taskListId, bool isSynced)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var cachedList = await db.CachedTaskLists.FindAsync(taskListId)
            ?? throw new InvalidOperationException($"Task list '{taskListId}' not found in cache.");

        cachedList.IsSynced = isSynced;
        cachedList.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Task list {ListId} isSynced={IsSynced}", taskListId, isSynced);
    }

    public async Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureListCacheValidAsync(db, taskListId);
        
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

    public async Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureCacheValidAsync(db);
        
        var today = await userTimeZoneService.GetTodayAsync();
        
        var tasks = await db.CachedTasks
            .Include(t => t.List)
            .Where(t => !t.IsDeleted && t.DueDate == today && t.List!.IsSynced)
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

    public async Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate, TimeOnly? reminderTime = null)
    {
        var created = await graphService.CreateTaskAsync(taskListId, title, dueDate, reminderTime);
        
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
            IsDeleted = false,
            LastSyncUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
        };
        
        db.CachedTasks.Add(cachedTask);
        await db.SaveChangesAsync();
        
        return created;
    }

    public async Task UpdateTaskStatusAsync(string taskListId, string taskId, bool completed)
    {
        await graphService.UpdateTaskStatusAsync(taskListId, taskId, completed);
        
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

    private async Task EnsureCacheValidAsync(AppDbContext db)
    {
        if (!await IsCacheStaleAsync(db))
            return;

        await _syncLock.WaitAsync();
        try
        {
            if (!await IsCacheStaleAsync(db))
                return;

            await SyncAsync(db);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task EnsureListsCacheValidAsync(AppDbContext db)
    {
        if (!await IsCacheStaleAsync(db))
            return;

        await _syncLock.WaitAsync();
        try
        {
            if (!await IsCacheStaleAsync(db))
                return;

            await SyncListsOnlyAsync(db);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task EnsureListCacheValidAsync(AppDbContext db, string taskListId)
    {
        if (!await IsListCacheStaleAsync(db, taskListId))
            return;

        await _syncLock.WaitAsync();
        try
        {
            if (!await IsListCacheStaleAsync(db, taskListId))
                return;

            var list = await db.CachedTaskLists.FindAsync(taskListId);
            if (list == null)
            {
                logger.LogInformation("List {ListId} not in cache, performing full sync", taskListId);
                await SyncAsync(db);
                return;
            }

            await SyncTasksForListAsync(db, list.Id, list.DeltaToken);
            list.LastSyncUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Single-list sync failed for {ListId}, falling back to full sync", taskListId);
            if (ShouldRebuildCache(ex))
                await ClearCacheAndInitialSyncAsync(db);
            else
                throw;
        }
        finally
        {
            _syncLock.Release();
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

    private async Task<bool> IsCacheStaleAsync(AppDbContext db)
    {
        var cacheMaxAge = TimeSpan.FromMinutes(_options.StalenessThresholdMinutes);
        var now = DateTime.UtcNow;
        
        var oldestSync = await db.CachedTaskLists
            .Where(l => l.IsSynced)
            .Select(l => (DateTime?)l.LastSyncUtc)
            .MinAsync();
        
        if (oldestSync == null)
        {
            logger.LogDebug("Cache is stale: no synced lists in cache");
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

    private async Task SyncAsync(AppDbContext db)
    {
        logger.LogInformation("Starting cache sync");
        
        try
        {
            var hasAnyLists = await db.CachedTaskLists.AnyAsync();
            
            if (!hasAnyLists)
            {
                logger.LogInformation("Cold cache: performing initial sync");
                await InitialSyncAsync(db);
            }
            else
            {
                logger.LogInformation("Warm cache: performing delta sync");
                await DeltaSyncAsync(db);
            }
            
            logger.LogInformation("Cache sync completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cache sync failed");
            
            if (ShouldRebuildCache(ex))
            {
                logger.LogWarning("Rebuilding cache due to invalid delta token or sync error");
                await ClearCacheAndInitialSyncAsync(db);
            }
            else
            {
                throw;
            }
        }
    }

    private async Task SyncListsOnlyAsync(AppDbContext db)
    {
        logger.LogInformation("Starting lists-only cache sync");

        try
        {
            var hasAnyLists = await db.CachedTaskLists.AnyAsync();

            if (!hasAnyLists)
            {
                var lists = await graphService.GetTaskListsAsync();
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
                    });
                }

                await db.SaveChangesAsync();
            }
            else
            {
                await SyncTaskListsAsync(db);

                // Update LastSyncUtc so staleness check passes without syncing tasks
                var lists = await db.CachedTaskLists
                    .Where(l => l.IsSynced)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                foreach (var list in lists)
                    list.LastSyncUtc = now;

                await db.SaveChangesAsync();
            }

            logger.LogInformation("Lists-only cache sync completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lists-only cache sync failed");

            if (ShouldRebuildCache(ex))
            {
                logger.LogWarning("Falling back to full sync due to error");
                await ClearCacheAndInitialSyncAsync(db);
            }
            else
            {
                throw;
            }
        }
    }

    private async Task InitialSyncAsync(AppDbContext db)
    {
        var lists = await graphService.GetTaskListsAsync();
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
            };
            
            db.CachedTaskLists.Add(cachedList);
            await db.SaveChangesAsync();
        }

        // Sync tasks for all lists in parallel
        await SyncTasksForListsBatchAsync(
            lists.Select(l => (l.Id, (string?)null)).ToList());
    }

    private async Task DeltaSyncAsync(AppDbContext db)
    {
        await SyncTaskListsAsync(db);

        var lists = await db.CachedTaskLists
            .Where(l => l.IsSynced)
            .Select(l => new { l.Id, l.DeltaToken })
            .ToListAsync();

        foreach (var list in lists)
        {
            if (list.DeltaToken == null)
                logger.LogWarning("List {ListId} has no delta token: a full sync will be performed and deleted tasks will not be detected", list.Id);
        }

        await SyncTasksForListsBatchAsync(
            lists.Select(l => (l.Id, l.DeltaToken)).ToList());
    }

    private async Task SyncTasksForListsBatchAsync(List<(string Id, string? DeltaToken)> lists)
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
                await ProcessTasksDeltaPagesAsync(scopedDb, kvp.Key, kvp.Value);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task SyncTaskListsAsync(AppDbContext db)
    {
        logger.LogDebug("Syncing task lists with delta query");

        var deltaTokenMetadata = await db.SyncMetadata.FindAsync(TaskListsDeltaTokenKey);
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
                                Key = TaskListsDeltaTokenKey,
                                Value = page.OdataDeltaLink,
                                UpdatedUtc = DateTime.UtcNow,
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing task lists delta");
            throw;
        }
    }

    private async Task SyncTasksForListAsync(AppDbContext scopedDb, string listId, string? deltaToken)
    {
        if (string.IsNullOrEmpty(deltaToken))
            logger.LogWarning("Syncing tasks for list {ListId}: no delta token available, performing full sync — @removed items are not returned by a full sync so deleted tasks will not be detected", listId);
        else
            logger.LogDebug("Syncing tasks for list {ListId} with delta token", listId);

        try
        {
            var firstPage = await graphClient.GetTasksDeltaPageAsync(listId, deltaToken);
            await ProcessTasksDeltaPagesAsync(scopedDb, listId, firstPage);
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
    private async Task ProcessTasksDeltaPagesAsync(AppDbContext scopedDb, string listId, GraphDeltaPage<Microsoft.Graph.Models.TodoTask> page)
    {
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

                        if (cachedTask == null)
                        {
                            logger.LogDebug("Adding new task {TaskId} to cache", graphTask.Id);
                            cachedTask = new CachedTask
                            {
                                Id = graphTask.Id!,
                                ListId = listId,
                                Title = graphTask.Title ?? "Untitled",
                                Body = graphTask.Body?.Content,
                                IsCompleted = graphTask.Status == Microsoft.Graph.Models.TaskStatus.Completed,
                                DueDate = dueDate,
                                Importance = graphTask.Importance?.ToString(),
                                IsDeleted = false,
                                LastSyncUtc = now,
                                CreatedUtc = now,
                                UpdatedUtc = now,
                            };
                            scopedDb.CachedTasks.Add(cachedTask);
                        }
                        else
                        {
                            logger.LogDebug("Updating task {TaskId} in cache", graphTask.Id);
                            cachedTask.Title = graphTask.Title ?? "Untitled";
                            cachedTask.Body = graphTask.Body?.Content;
                            cachedTask.IsCompleted = graphTask.Status == Microsoft.Graph.Models.TaskStatus.Completed;
                            cachedTask.DueDate = dueDate;
                            cachedTask.Importance = graphTask.Importance?.ToString();
                            cachedTask.IsDeleted = false;
                            cachedTask.UpdatedUtc = now;
                            cachedTask.LastSyncUtc = now;
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

    private async Task ClearCacheAndInitialSyncAsync(AppDbContext db)
    {
        logger.LogInformation("Clearing cache and performing full rebuild");
        
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CachedTasks");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CachedTaskLists");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM SyncMetadata WHERE Key = {0}", TaskListsDeltaTokenKey);
        
        await InitialSyncAsync(db);
    }

    private static bool ShouldRebuildCache(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("410") || 
               message.Contains("Gone") || 
               message.Contains("delta") && message.Contains("invalid");
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
