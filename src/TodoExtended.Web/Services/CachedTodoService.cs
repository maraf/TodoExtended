using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Extensions.Options;
using TodoExtended.Web.Data;
using System.Globalization;

namespace TodoExtended.Web.Services;

public class CachedTodoService(
    GraphTodoService graphService,
    AppDbContext db,
    GraphServiceClient graphClient,
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
        await EnsureListsCacheValidAsync();
        
        return await db.CachedTaskLists
            .Where(l => l.IsSynced)
            .OrderBy(l => l.DisplayName)
            .Select(l => new TodoTaskList(l.Id, l.DisplayName, l.IsSynced))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TodoTaskList>> GetNotSyncedTaskListsAsync()
    {
        return await db.CachedTaskLists
            .Where(l => !l.IsSynced)
            .OrderBy(l => l.DisplayName)
            .Select(l => new TodoTaskList(l.Id, l.DisplayName, l.IsSynced))
            .ToListAsync();
    }

    public async Task SetTaskListSyncedAsync(string taskListId, bool isSynced)
    {
        var cachedList = await db.CachedTaskLists.FindAsync(taskListId)
            ?? throw new InvalidOperationException($"Task list '{taskListId}' not found in cache.");

        cachedList.IsSynced = isSynced;
        cachedList.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Task list {ListId} isSynced={IsSynced}", taskListId, isSynced);
    }

    public async Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId)
    {
        await EnsureListCacheValidAsync(taskListId);
        
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
        await EnsureCacheValidAsync();
        
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
        
        var cachedTask = await db.CachedTasks.FindAsync(taskId);
        if (cachedTask != null)
        {
            cachedTask.IsCompleted = completed;
            cachedTask.UpdatedUtc = DateTime.UtcNow;
            cachedTask.LastSyncUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private async Task EnsureCacheValidAsync()
    {
        if (!await IsCacheStaleAsync())
            return;

        await _syncLock.WaitAsync();
        try
        {
            if (!await IsCacheStaleAsync())
                return;

            await SyncAsync();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task EnsureListsCacheValidAsync()
    {
        if (!await IsCacheStaleAsync())
            return;

        await _syncLock.WaitAsync();
        try
        {
            if (!await IsCacheStaleAsync())
                return;

            await SyncListsOnlyAsync();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task EnsureListCacheValidAsync(string taskListId)
    {
        if (!await IsListCacheStaleAsync(taskListId))
            return;

        await _syncLock.WaitAsync();
        try
        {
            if (!await IsListCacheStaleAsync(taskListId))
                return;

            var list = await db.CachedTaskLists.FindAsync(taskListId);
            if (list == null)
            {
                logger.LogInformation("List {ListId} not in cache, performing full sync", taskListId);
                await SyncAsync();
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
                await ClearCacheAndInitialSyncAsync();
            else
                throw;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<bool> IsListCacheStaleAsync(string taskListId)
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

    private async Task<bool> IsCacheStaleAsync()
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

    private async Task SyncAsync()
    {
        logger.LogInformation("Starting cache sync");
        
        try
        {
            var hasAnyLists = await db.CachedTaskLists.AnyAsync();
            
            if (!hasAnyLists)
            {
                logger.LogInformation("Cold cache: performing initial sync");
                await InitialSyncAsync();
            }
            else
            {
                logger.LogInformation("Warm cache: performing delta sync");
                await DeltaSyncAsync();
            }
            
            logger.LogInformation("Cache sync completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cache sync failed");
            
            if (ShouldRebuildCache(ex))
            {
                logger.LogWarning("Rebuilding cache due to invalid delta token or sync error");
                await ClearCacheAndInitialSyncAsync();
            }
            else
            {
                throw;
            }
        }
    }

    private async Task SyncListsOnlyAsync()
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
                await SyncTaskListsAsync();

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
                await ClearCacheAndInitialSyncAsync();
            }
            else
            {
                throw;
            }
        }
    }

    private async Task InitialSyncAsync()
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
        await SyncTasksForListsInParallelAsync(
            lists.Select(l => (l.Id, (string?)null)).ToList());
    }

    private async Task DeltaSyncAsync()
    {
        await SyncTaskListsAsync();

        var lists = await db.CachedTaskLists
            .Where(l => l.IsSynced)
            .Select(l => new { l.Id, l.DeltaToken })
            .ToListAsync();

        await SyncTasksForListsInParallelAsync(
            lists.Select(l => (l.Id, l.DeltaToken)).ToList());
    }

    private async Task SyncTasksForListsInParallelAsync(List<(string Id, string? DeltaToken)> lists)
    {
        if (lists.Count == 0) return;

        using var throttle = new SemaphoreSlim(_options.MaxParallelListSync);
        var tasks = lists.Select(async list =>
        {
            await throttle.WaitAsync();
            try
            {
                await using var scopedDb = await dbContextFactory.CreateDbContextAsync();
                await SyncTasksForListAsync(scopedDb, list.Id, list.DeltaToken);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task SyncTaskListsAsync()
    {
        logger.LogDebug("Syncing task lists with delta query");

        var deltaTokenMetadata = await db.SyncMetadata.FindAsync(TaskListsDeltaTokenKey);
        var deltaToken = deltaTokenMetadata?.Value;

        try
        {
            var response = string.IsNullOrEmpty(deltaToken)
                ? await graphClient.Me.Todo.Lists.Delta.GetAsDeltaGetResponseAsync()
                : await graphClient.Me.Todo.Lists.Delta
                    .WithUrl(deltaToken)
                    .GetAsDeltaGetResponseAsync();

            while (response != null)
            {
                if (response.Value != null)
                {
                    foreach (var graphList in response.Value)
                    {
                        if (graphList.AdditionalData?.ContainsKey("@removed") == true)
                        {
                            var cachedList = await db.CachedTaskLists.FindAsync(graphList.Id);
                            if (cachedList != null)
                            {
                                logger.LogDebug("Removing task list {ListId} from cache", graphList.Id);
                                db.CachedTaskLists.Remove(cachedList);
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

                if (!string.IsNullOrEmpty(response.OdataNextLink))
                {
                    logger.LogDebug("Fetching next page of task lists delta");
                    response = await graphClient.Me.Todo.Lists.Delta
                        .WithUrl(response.OdataNextLink)
                        .GetAsDeltaGetResponseAsync();
                }
                else
                {
                    if (!string.IsNullOrEmpty(response.OdataDeltaLink))
                    {
                        logger.LogDebug("Storing task lists delta token");
                        if (deltaTokenMetadata == null)
                        {
                            deltaTokenMetadata = new SyncMetadata
                            {
                                Key = TaskListsDeltaTokenKey,
                                Value = response.OdataDeltaLink,
                                UpdatedUtc = DateTime.UtcNow,
                            };
                            db.SyncMetadata.Add(deltaTokenMetadata);
                        }
                        else
                        {
                            deltaTokenMetadata.Value = response.OdataDeltaLink;
                            deltaTokenMetadata.UpdatedUtc = DateTime.UtcNow;
                        }
                        await db.SaveChangesAsync();
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
        logger.LogDebug("Syncing tasks for list {ListId} with delta token: {HasToken}", listId, !string.IsNullOrEmpty(deltaToken));

        try
        {
            var response = string.IsNullOrEmpty(deltaToken)
                ? await graphClient.Me.Todo.Lists[listId].Tasks.Delta.GetAsDeltaGetResponseAsync()
                : await graphClient.Me.Todo.Lists[listId].Tasks.Delta
                    .WithUrl(deltaToken)
                    .GetAsDeltaGetResponseAsync();

            while (response != null)
            {
                if (response.Value != null)
                {
                    foreach (var graphTask in response.Value)
                    {
                        if (graphTask.AdditionalData?.ContainsKey("@removed") == true)
                        {
                            var cachedTask = await scopedDb.CachedTasks.FindAsync(graphTask.Id);
                            if (cachedTask != null)
                            {
                                logger.LogDebug("Soft deleting task {TaskId} from cache", graphTask.Id);
                                cachedTask.IsDeleted = true;
                                cachedTask.UpdatedUtc = DateTime.UtcNow;
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

                if (!string.IsNullOrEmpty(response.OdataNextLink))
                {
                    logger.LogDebug("Fetching next page of tasks delta for list {ListId}", listId);
                    response = await graphClient.Me.Todo.Lists[listId].Tasks.Delta
                        .WithUrl(response.OdataNextLink)
                        .GetAsDeltaGetResponseAsync();
                }
                else
                {
                    if (!string.IsNullOrEmpty(response.OdataDeltaLink))
                    {
                        logger.LogDebug("Storing delta token for list {ListId}", listId);
                        var cachedList = await scopedDb.CachedTaskLists.FindAsync(listId);
                        if (cachedList != null)
                        {
                            cachedList.DeltaToken = response.OdataDeltaLink;
                            cachedList.LastSyncUtc = DateTime.UtcNow;
                            await scopedDb.SaveChangesAsync();
                        }
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing tasks for list {ListId}", listId);
            throw;
        }
    }

    private async Task ClearCacheAndInitialSyncAsync()
    {
        logger.LogInformation("Clearing cache and performing full rebuild");
        
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CachedTasks");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CachedTaskLists");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM SyncMetadata WHERE Key = {0}", TaskListsDeltaTokenKey);
        
        await InitialSyncAsync();
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
