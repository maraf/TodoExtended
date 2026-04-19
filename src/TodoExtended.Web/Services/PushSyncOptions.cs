namespace TodoExtended.Web.Services;

public class PushSyncOptions
{
    public const string SectionName = "PushSync";

    public bool Enabled { get; set; }

    public List<string> AllowedUsers { get; set; } = [];

    public int SyncIntervalSeconds { get; set; } = 60;

    public int HealthTtlMinutes { get; set; } = 10;

    public string? NotificationUrl { get; set; }

    public int SubscriptionLifetimeMinutes { get; set; } = 180;

    public int RenewBeforeMinutes { get; set; } = 30;
}
