namespace TodoExtended.Web.Data;

public class DistributedCacheEntry
{
    public required string Key { get; set; }
    public required byte[] Value { get; set; }
    public DateTimeOffset? AbsoluteExpiration { get; set; }
    public double? SlidingExpirationInSeconds { get; set; }
    public DateTimeOffset? LastAccessed { get; set; }
}
