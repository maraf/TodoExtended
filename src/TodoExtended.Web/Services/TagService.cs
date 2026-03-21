using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class TagService(IDbContextFactory<AppDbContext> dbContextFactory) : ITagService
{
    public async Task<IReadOnlyList<TagWithCount>> GetTagsAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        // Group CachedTag rows by Name, count open (non-completed, non-deleted, synced) tasks per tag
        var tagCounts = await db.CachedTags
            .AsNoTracking()
            .Where(ct => ct.UserId == userId && ct.Task!.List!.IsSynced && !ct.Task.IsDeleted)
            .GroupBy(ct => ct.Name)
            .Select(g => new
            {
                Tag = g.Key,
                TaskCount = g.Count(ct => !ct.Task!.IsCompleted),
            })
            .ToListAsync();

        return tagCounts
            .OrderByDescending(t => t.TaskCount)
            .ThenBy(t => t.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(t => new TagWithCount(t.Tag, t.TaskCount))
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTaskWithList>> GetTasksByTagAsync(string tag, string userId)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return [];

        await using var db = await dbContextFactory.CreateDbContextAsync();

        var normalizedTag = tag.ToLowerInvariant();

        var tasks = await db.CachedTags
            .AsNoTracking()
            .Where(ct => ct.UserId == userId && ct.Name == normalizedTag
                && !ct.Task!.IsDeleted && ct.Task.List!.IsSynced)
            .Select(ct => new TodoTaskWithList(
                ct.Task!.Id, ct.Task.Title, ct.Task.Body, ct.Task.IsCompleted,
                ct.Task.DueDate, ct.Task.Importance, ct.Task.ListId, ct.Task.List!.DisplayName))
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
            .Where(ct => ct.UserId == userId && ct.IsPinned)
            .Select(ct => ct.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
    }

    public async Task SetTagPinnedAsync(string tag, bool pinned, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var normalizedTag = tag.ToLowerInvariant();

        await db.CachedTags
            .Where(ct => ct.UserId == userId && ct.Name == normalizedTag)
            .ExecuteUpdateAsync(s => s.SetProperty(ct => ct.IsPinned, pinned));
    }
}
