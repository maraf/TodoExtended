namespace TodoExtended.Web.Data;

public class CachedTaskList
{
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public string? DeltaToken { get; set; }
    public DateTime LastSyncUtc { get; set; }
    public bool IsSynced { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public required string UserId { get; set; }
    
    public ICollection<CachedTask> Tasks { get; set; } = [];
}
