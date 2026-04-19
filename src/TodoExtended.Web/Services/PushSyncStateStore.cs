using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public sealed class PushSyncStateStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<PushSyncStateStore> logger)
{
    private const string ClientStatePrefix = "pushsync";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PushSyncState> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await GetAsync(db, userId, cancellationToken);
    }

    public async Task<PushSyncState> GetAsync(AppDbContext db, string userId, CancellationToken cancellationToken = default)
    {
        var key = PushSyncMetadataKeys.State(userId);
        var rawValue = await db.SyncMetadata
            .AsNoTracking()
            .Where(m => m.Key == key)
            .Select(m => m.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(rawValue))
            return new PushSyncState { UserId = userId };

        try
        {
            var state = JsonSerializer.Deserialize<PushSyncState>(rawValue, JsonOptions)
                ?? new PushSyncState();
            state.UserId = userId;
            state.Subscriptions ??= [];
            return state;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Resetting unreadable push-sync state for user {UserId}", userId);
            return new PushSyncState
            {
                UserId = userId,
                PendingRefresh = true,
                LastError = "Stored push-sync state was unreadable and has been reset."
            };
        }
    }

    public async Task SaveAsync(string userId, PushSyncState state, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await SaveAsync(db, userId, state, cancellationToken);
    }

    public async Task SaveAsync(AppDbContext db, string userId, PushSyncState state, CancellationToken cancellationToken = default)
    {
        state.UserId = userId;
        state.Subscriptions = state.Subscriptions
            .Where(s => !string.IsNullOrWhiteSpace(s.Resource))
            .GroupBy(s => s.Resource, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(s => s.ExpiresUtc).First())
            .ToList();

        var value = JsonSerializer.Serialize(state, JsonOptions);
        if (value.Length > 4000)
        {
            state.Subscriptions = state.Subscriptions
                .OrderByDescending(s => s.ExpiresUtc)
                .Take(20)
                .ToList();
            value = JsonSerializer.Serialize(state, JsonOptions);
        }

        var entry = await db.SyncMetadata.FindAsync([PushSyncMetadataKeys.State(userId)], cancellationToken);
        if (entry == null)
        {
            entry = new SyncMetadata
            {
                Key = PushSyncMetadataKeys.State(userId),
                Value = value,
                UpdatedUtc = DateTime.UtcNow,
                UserId = userId,
            };
            db.SyncMetadata.Add(entry);
        }
        else
        {
            entry.Value = value;
            entry.UpdatedUtc = DateTime.UtcNow;
            entry.UserId = userId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryRecordNotificationAsync(
        string userId,
        string? subscriptionId,
        string? clientState,
        string? resource,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await GetAsync(db, userId, cancellationToken);

        var subscription = state.Subscriptions.FirstOrDefault(s =>
            (!string.IsNullOrWhiteSpace(subscriptionId) &&
             string.Equals(s.SubscriptionId, subscriptionId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(clientState) &&
             string.Equals(s.ClientState, clientState, StringComparison.Ordinal)));

        if (subscription == null)
            return false;

        if (!string.IsNullOrWhiteSpace(resource))
            subscription.Resource = resource;

        state.PendingRefresh = true;
        state.LastNotificationUtc = DateTime.UtcNow;
        state.LastError = null;

        await SaveAsync(db, userId, state, cancellationToken);
        return true;
    }

    public static string CreateClientState(string userId) =>
        $"{ClientStatePrefix}|{userId}|{Guid.NewGuid():N}";

    public static bool TryParseUserId(string? clientState, out string? userId)
    {
        userId = null;

        if (string.IsNullOrWhiteSpace(clientState))
            return false;

        var parts = clientState.Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], ClientStatePrefix, StringComparison.Ordinal))
            return false;

        userId = parts[1];
        return !string.IsNullOrWhiteSpace(userId);
    }
}

public sealed class PushSyncState
{
    public string UserId { get; set; } = string.Empty;
    public bool PendingRefresh { get; set; }
    public DateTime? LastNotificationUtc { get; set; }
    public string? LastError { get; set; }
    public List<PushSyncSubscriptionState> Subscriptions { get; set; } = [];
}

public sealed class PushSyncSubscriptionState
{
    public string Resource { get; set; } = string.Empty;
    public string? SubscriptionId { get; set; }
    public string ClientState { get; set; } = string.Empty;
    public DateTime? ExpiresUtc { get; set; }
}
