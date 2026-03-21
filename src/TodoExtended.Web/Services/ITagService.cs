namespace TodoExtended.Web.Services;

public record TagWithCount(string Tag, int TaskCount);

public interface ITagService
{
    /// <summary>Returns all tags with non-completed task counts for the user.</summary>
    Task<IReadOnlyList<TagWithCount>> GetTagsAsync(string userId);

    /// <summary>Returns non-deleted tasks that have a given tag.</summary>
    Task<IReadOnlyList<TodoTaskWithList>> GetTasksByTagAsync(string tag, string userId);

    /// <summary>Returns the pinned tags for the user.</summary>
    Task<IReadOnlyList<string>> GetPinnedTagsAsync(string userId);

    /// <summary>Pins or unpins a tag for the user.</summary>
    Task SetTagPinnedAsync(string tag, bool pinned, string userId);
}
