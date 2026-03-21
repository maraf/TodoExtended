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
                TaskCount = t.TaskTags.Count(tt =>
                    !tt.Task!.IsDeleted && tt.Task.List!.IsSynced && !tt.Task.IsCompleted),
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

        var tasks = await db.CachedTaskTags
            .AsNoTracking()
            .Where(tt => tt.TagName == normalizedTag && tt.TagUserId == userId
                && !tt.Task!.IsDeleted && tt.Task.List!.IsSynced)
            .Select(tt => new TodoTaskWithList(
                tt.Task!.Id, tt.Task.Title, tt.Task.Body, tt.Task.IsCompleted,
                tt.Task.DueDate, tt.Task.Importance, tt.Task.ListId, tt.Task.List!.DisplayName))
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
