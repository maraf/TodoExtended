namespace TodoExtended.Web.Data;

public class CachedTaskTag
{
    public required string TagName { get; set; }    // Part of PK, FK to CachedTag.Name
    public required string TagUserId { get; set; }  // Part of PK, FK to CachedTag.UserId
    public required string TaskId { get; set; }     // Part of PK, FK to CachedTask.Id

    public CachedTag? Tag { get; set; }
    public CachedTask? Task { get; set; }
}
