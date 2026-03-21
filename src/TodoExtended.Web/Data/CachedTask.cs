namespace TodoExtended.Web.Data;

public class CachedTask
{
    public required string Id { get; set; }
    public required string ListId { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public bool IsCompleted { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Importance { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime LastSyncUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public required string UserId { get; set; }
    
    public CachedTaskList? List { get; set; }
    public ICollection<CachedTaskTag> TaskTags { get; set; } = [];
}
