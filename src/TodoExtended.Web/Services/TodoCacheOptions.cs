namespace TodoExtended.Web.Services;

public class TodoCacheOptions
{
    public int StalenessThresholdMinutes { get; set; } = 5;
    public bool EnableBackgroundSync { get; set; } = true;
    public int SoftDeleteRetentionDays { get; set; } = 30;
    public int MaxParallelListSync { get; set; } = 10;
}
