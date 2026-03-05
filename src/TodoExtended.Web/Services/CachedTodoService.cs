using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Extensions.Options;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class CachedTodoService(
    GraphTodoService graphService,
    AppDbContext db,
    GraphServiceClient graphClient,
    IOptions<TodoCacheOptions> options,
    ILogger<CachedTodoService> logger) : ITodoService
{
    private readonly TodoCacheOptions _options = options.Value;
    private static readonly SemaphoreSlim _syncLock = new(1, 1);
    private static readonly string TaskListsDeltaTokenKey = "TaskListsDeltaToken";

    public async Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync()
    {
        await EnsureCacheValidAsync();
        
        return await db.CachedTaskLists
            .OrderBy(l => l.DisplayName)
            .Select(l => new TodoTaskList(l.Id, l.DisplayName))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId)
    {
        await EnsureCacheValidAsync();
        
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

    public async Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate)
    {
        var created = await graphService.CreateTaskAsync(taskListId, title, dueDate);
        
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

    private async Task<bool> IsCacheStaleAsync()
    {
        var cacheMaxAge = TimeSpan.FromMinutes(_options.StalenessThresholdMinutes);
        var now = DateTime.UtcNow;
        
        var oldestSync = await db.CachedTaskLists
            .Select(l => (DateTime?)l.LastSyncUtc)
            .MinAsync();
        
        if (oldestSync == null)
        {
            logger.LogDebug("Cache is stale: no lists in cache");
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
                DeltaToken = null,
                LastSyncUtc = now,
                CreatedUtc = now,
                UpdatedUtc = now,
            };
            
            db.CachedTaskLists.Add(cachedList);
            await db.SaveChangesAsync();

            await SyncTasksForListAsync(list.Id, null);
        }
    }

    private async Task DeltaSyncAsync()
    {
        await SyncTaskListsAsync();

        var lists = await db.CachedTaskLists.ToListAsync();
        foreach (var list in lists)
        {
            await SyncTasksForListAsync(list.Id, list.DeltaToken);
        }
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

    private async Task SyncTasksForListAsync(string listId, string? deltaToken)
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
                            var cachedTask = await db.CachedTasks.FindAsync(graphTask.Id);
                            if (cachedTask != null)
                            {
                                logger.LogDebug("Soft deleting task {TaskId} from cache", graphTask.Id);
                                cachedTask.IsDeleted = true;
                                cachedTask.UpdatedUtc = DateTime.UtcNow;
                            }
                        }
                        else
                        {
                            var cachedTask = await db.CachedTasks.FindAsync(graphTask.Id);
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
                                db.CachedTasks.Add(cachedTask);
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
                    
                    await db.SaveChangesAsync();
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
                        var cachedList = await db.CachedTaskLists.FindAsync(listId);
                        if (cachedList != null)
                        {
                            cachedList.DeltaToken = response.OdataDeltaLink;
                            cachedList.LastSyncUtc = DateTime.UtcNow;
                            await db.SaveChangesAsync();
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

    private static DateOnly? ParseDueDate(Microsoft.Graph.Models.DateTimeTimeZone? dueDateTime)
    {
        if (dueDateTime?.DateTime is null) return null;

        var dt = DateTime.Parse(dueDateTime.DateTime, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None);

        if (!string.IsNullOrEmpty(dueDateTime.TimeZone))
        {
            var sourceZone = TimeZoneInfo.FindSystemTimeZoneById(dueDateTime.TimeZone);
            dt = TimeZoneInfo.ConvertTime(dt, sourceZone, TimeZoneInfo.Local);
        }

        return DateOnly.FromDateTime(dt);
    }

    private static int ImportanceSortOrder(string? importance) => importance?.ToLowerInvariant() switch
    {
        "high" => 0,
        "normal" => 1,
        "low" => 2,
        _ => 1,
    };
}
