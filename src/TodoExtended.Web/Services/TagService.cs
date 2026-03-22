using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class TagService(IDbContextFactory<AppDbContext> dbContextFactory) : ITagService
{
    public async Task<IReadOnlyList<TagWithCount>> GetTagsAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var tagCounts = await db.CachedTags
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new
            {
                Tag = t.Name,
                TaskCount = t.Tasks.Count(task =>
                    !task.IsDeleted && task.List!.IsSynced && !task.IsCompleted),
            })
            .ToListAsync();

        return tagCounts
            .OrderBy(t => t.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(t => new TagWithCount(t.Tag, t.TaskCount))
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTaskWithList>> GetTasksByTagAsync(string tag, string userId)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return [];

        await using var db = await dbContextFactory.CreateDbContextAsync();

        var normalizedTag = tag.ToLowerInvariant();

        var tasks = await db.CachedTasks
            .AsNoTracking()
            .Where(task => task.UserId == userId && !task.IsDeleted && task.List!.IsSynced
                && task.Tags.Any(t => t.Name == normalizedTag))
            .Select(task => new TodoTaskWithList(
                task.Id, task.Title, task.Body, task.IsCompleted,
                task.DueDate, task.Importance, task.ListId, task.List!.DisplayName))
            .ToListAsync();

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetPinnedTagsAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        return await db.CachedTags
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.IsPinned)
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToListAsync();
    }

    public async Task SetTagPinnedAsync(string tag, bool pinned, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var normalizedTag = tag.ToLowerInvariant();

        await db.CachedTags
            .Where(t => t.UserId == userId && t.Name == normalizedTag)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsPinned, pinned));
    }
}
