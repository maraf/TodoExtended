namespace TodoExtended.Web.Data;

public class CachedTag
{
    public required string Name { get; set; }    // Lowercase tag name (part of PK)
    public required string UserId { get; set; }  // FK to User (part of PK)
    public bool IsPinned { get; set; }

    public User? User { get; set; }
    public ICollection<CachedTask> Tasks { get; set; } = [];
}
