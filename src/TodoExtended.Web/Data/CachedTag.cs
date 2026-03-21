namespace TodoExtended.Web.Data;

public class CachedTag
{
    public required string Name { get; set; }     // Lowercase tag name
    public required string TaskId { get; set; }   // FK to CachedTask
    public required string UserId { get; set; }   // FK to User (denormalized for fast queries)
    public bool IsPinned { get; set; }            // True when user has pinned this tag

    public CachedTask? Task { get; set; }
    public User? User { get; set; }
}
