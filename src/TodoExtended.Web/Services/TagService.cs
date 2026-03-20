using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class TagService(IDbContextFactory<AppDbContext> dbContextFactory) : ITagService
{
    public async Task<IReadOnlyList<TagWithCount>> GetTagsAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var tasks = await db.CachedTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId && !t.IsDeleted && t.Tags != null && t.List!.IsSynced)
            .Select(t => new { t.IsCompleted, t.Tags })
            .ToListAsync();

        var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var task in tasks)
        {
            var tags = TagExtractor.ParseTags(task.Tags);
            foreach (var tag in tags)
            {
                if (!tagCounts.ContainsKey(tag))
                    tagCounts[tag] = 0;

                if (!task.IsCompleted)
                    tagCounts[tag]++;
            }
        }

        return tagCounts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new TagWithCount(kvp.Key, kvp.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<TodoTaskWithList>> GetTasksByTagAsync(string tag, string userId)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return [];

        await using var db = await dbContextFactory.CreateDbContextAsync();

        var normalizedTag = tag.ToLowerInvariant();

        // Use LIKE to find candidate tasks, then filter in-memory to avoid false positives
        // (e.g. searching "work" could match stored tag "overwork" without word-boundary checks).
        var tasks = await db.CachedTasks
            .AsNoTracking()
            .Include(t => t.List)
            .Where(t => t.UserId == userId && !t.IsDeleted && t.List!.IsSynced && t.Tags != null
                && (EF.Functions.Like(t.Tags, normalizedTag)                    // exact match (only tag)
                    || EF.Functions.Like(t.Tags, normalizedTag + " %")          // first tag
                    || EF.Functions.Like(t.Tags, "% " + normalizedTag)          // last tag
                    || EF.Functions.Like(t.Tags, "% " + normalizedTag + " %"))) // middle tag
            .ToListAsync();

        return tasks
            .Where(t => TagExtractor.ParseTags(t.Tags).Contains(normalizedTag, StringComparer.OrdinalIgnoreCase))
            .Select(t => new TodoTaskWithList(
                t.Id, t.Title, t.Body, t.IsCompleted, t.DueDate, t.Importance,
                t.ListId, t.List!.DisplayName))
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetPinnedTagsAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        return DeserializePinnedTags(user?.PinnedTags);
    }

    public async Task SetTagPinnedAsync(string tag, bool pinned, string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user is null) return;

        var current = DeserializePinnedTags(user.PinnedTags).ToList();

        if (pinned)
        {
            if (!current.Contains(tag, StringComparer.OrdinalIgnoreCase))
                current.Add(tag.ToLowerInvariant());
        }
        else
        {
            current.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        }

        user.PinnedTags = current.Count == 0 ? null : JsonSerializer.Serialize(current);
        await db.SaveChangesAsync();
    }

    private static IReadOnlyList<string> DeserializePinnedTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
