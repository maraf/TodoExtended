using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class PushSyncHealthService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<PushSyncOptions> options,
    PushSyncStateStore stateStore,
    ILogger<PushSyncHealthService> logger) : IPushSyncHealthService
{
    private readonly PushSyncOptions _options = options.Value;

    public async Task<bool> IsHealthyAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !HasNotificationUrl(_options.NotificationUrl))
            return false;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var successKey = PushSyncMetadataKeys.LastSuccess(userId);
        var failureKey = PushSyncMetadataKeys.LastFailure(userId);

        var entries = await db.SyncMetadata
            .Where(m => m.Key == successKey || m.Key == failureKey)
            .Select(m => new { m.Key, m.Value })
            .ToListAsync(cancellationToken);

        var lastSuccess = ParseTimestamp(entries.FirstOrDefault(e => e.Key == successKey)?.Value);
        if (lastSuccess == null)
            return false;

        var lastFailure = ParseTimestamp(entries.FirstOrDefault(e => e.Key == failureKey)?.Value);
        if (lastFailure != null && lastFailure >= lastSuccess)
            return false;

        var state = await stateStore.GetAsync(db, userId, cancellationToken);
        if (state.PendingRefresh || state.Subscriptions.Count == 0 || !string.IsNullOrWhiteSpace(state.LastError))
            return false;

        var renewWindow = TimeSpan.FromMinutes(Math.Max(1, _options.RenewBeforeMinutes));
        if (state.Subscriptions.Any(s => !s.ExpiresUtc.HasValue || s.ExpiresUtc.Value <= DateTime.UtcNow.Add(renewWindow)))
            return false;

        var healthWindow = TimeSpan.FromMinutes(Math.Max(1, _options.HealthTtlMinutes));
        var isHealthy = DateTime.UtcNow - lastSuccess.Value <= healthWindow;

        if (!isHealthy)
        {
            logger.LogDebug("Push sync health expired for user {UserId}: last success {LastSuccess}", userId, lastSuccess);
        }

        return isHealthy;
    }

    public async Task RecordSuccessAsync(string userId, CancellationToken cancellationToken = default)
    {
        await UpsertTimestampAsync(PushSyncMetadataKeys.LastSuccess(userId), userId, DateTime.UtcNow, cancellationToken);

        var state = await stateStore.GetAsync(userId, cancellationToken);
        state.PendingRefresh = false;
        state.LastError = null;
        await stateStore.SaveAsync(userId, state, cancellationToken);
    }

    public async Task RecordFailureAsync(string userId, CancellationToken cancellationToken = default)
    {
        await UpsertTimestampAsync(PushSyncMetadataKeys.LastFailure(userId), userId, DateTime.UtcNow, cancellationToken);

        var state = await stateStore.GetAsync(userId, cancellationToken);
        state.PendingRefresh = true;
        state.LastError ??= "Push-sync background refresh failed.";
        await stateStore.SaveAsync(userId, state, cancellationToken);
    }

    private static DateTime? ParseTimestamp(string? value) =>
        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    private static string FormatTimestamp(DateTime value) => value.ToString("O");

    private static bool HasNotificationUrl(string? notificationUrl) =>
        Uri.TryCreate(notificationUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private async Task UpsertTimestampAsync(
        string key,
        string userId,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.SyncMetadata.FindAsync([key], cancellationToken);

        if (entry == null)
        {
            entry = new SyncMetadata
            {
                Key = key,
                Value = FormatTimestamp(timestamp),
                UpdatedUtc = timestamp,
                UserId = userId,
            };
            db.SyncMetadata.Add(entry);
        }
        else
        {
            entry.Value = FormatTimestamp(timestamp);
            entry.UpdatedUtc = timestamp;
            entry.UserId = userId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
