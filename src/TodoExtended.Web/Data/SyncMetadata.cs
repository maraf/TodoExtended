namespace TodoExtended.Web.Data;

public class SyncMetadata
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
