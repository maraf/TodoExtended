using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class PushSyncBackgroundService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ApiKeyGraphClientFactory graphClientFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<TodoCacheOptions> todoCacheOptions,
    IOptionsMonitor<PushSyncOptions> pushSyncOptions,
    IPushSyncGate pushSyncGate,
    IPushSyncHealthService pushSyncHealthService,
    PushSyncStateStore stateStore,
    ILoggerFactory loggerFactory,
    ILogger<PushSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Push sync background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = pushSyncOptions.CurrentValue;
            var delay = TimeSpan.FromSeconds(Math.Max(10, options.SyncIntervalSeconds));

            try
            {
                if (options.Enabled)
                    await RunSyncCycleAsync(options, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Push sync cycle failed");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task RunSyncCycleAsync(PushSyncOptions options, CancellationToken cancellationToken)
    {
        if (!HasNotificationUrl(options.NotificationUrl))
        {
            logger.LogDebug("Push sync is enabled but no notification URL is configured; request-time delta sync will remain the fallback path");
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var users = await db.Users
            .AsNoTracking()
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var preferredUsername = await db.SyncMetadata
                .Where(m => m.Key == PushSyncMetadataKeys.PreferredUsername(user.Id))
                .Select(m => m.Value)
                .FirstOrDefaultAsync(cancellationToken);

            if (!pushSyncGate.IsEligible(user.Email, preferredUsername))
                continue;

            var state = await stateStore.GetAsync(db, user.Id, cancellationToken);

            try
            {
                var graphTodoClient = new HttpGraphTodoClient(graphClientFactory.CreateForUser(user.Id));
                var subscriptionsChanged = await EnsureSubscriptionsAsync(
                    user.Id,
                    graphTodoClient,
                    db,
                    state,
                    options,
                    cancellationToken);

                if (subscriptionsChanged)
                    await stateStore.SaveAsync(db, user.Id, state, cancellationToken);

                var shouldRefresh = state.PendingRefresh || !await pushSyncHealthService.IsHealthyAsync(user.Id, cancellationToken);
                if (!shouldRefresh)
                    continue;

                await RunBackgroundRefreshAsync(user.Id, graphTodoClient, cancellationToken);

                state.PendingRefresh = false;
                state.LastError = null;
                await stateStore.SaveAsync(db, user.Id, state, cancellationToken);
                await pushSyncHealthService.RecordSuccessAsync(user.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Push sync failed for user {UserId}", user.Id);
                state.PendingRefresh = true;
                state.LastError = ex.Message;
                await stateStore.SaveAsync(db, user.Id, state, cancellationToken);
                await pushSyncHealthService.RecordFailureAsync(user.Id, cancellationToken);
            }
        }
    }

    private async Task<bool> EnsureSubscriptionsAsync(
        string userId,
        IGraphTodoClient graphTodoClient,
        AppDbContext db,
        PushSyncState state,
        PushSyncOptions options,
        CancellationToken cancellationToken)
    {
        var desiredResources = await GetDesiredResourcesAsync(db, userId, cancellationToken);
        var changed = false;

        foreach (var resource in desiredResources)
        {
            var current = state.Subscriptions.FirstOrDefault(s => string.Equals(s.Resource, resource, StringComparison.OrdinalIgnoreCase));
            var shouldCreate = current?.SubscriptionId is null;
            var shouldRenew = current?.SubscriptionId is not null &&
                              (!current.ExpiresUtc.HasValue ||
                               current.ExpiresUtc.Value <= DateTime.UtcNow.AddMinutes(Math.Max(1, options.RenewBeforeMinutes)));

            if (!shouldCreate && !shouldRenew)
                continue;

            current ??= new PushSyncSubscriptionState
            {
                Resource = resource,
                ClientState = PushSyncStateStore.CreateClientState(userId),
            };

            var expiration = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(options.SubscriptionLifetimeMinutes, 15, 4230));
            Microsoft.Graph.Models.Subscription? subscription;

            if (shouldCreate)
            {
                subscription = await graphTodoClient.CreateSubscriptionAsync(
                    resource,
                    options.NotificationUrl!,
                    current.ClientState,
                    expiration,
                    cancellationToken);
            }
            else
            {
                subscription = await graphTodoClient.RenewSubscriptionAsync(
                    current.SubscriptionId!,
                    expiration,
                    cancellationToken);

                if (subscription == null)
                {
                    subscription = await graphTodoClient.CreateSubscriptionAsync(
                        resource,
                        options.NotificationUrl!,
                        current.ClientState,
                        expiration,
                        cancellationToken);
                }
            }

            current.SubscriptionId = subscription.Id;
            current.ExpiresUtc = (subscription.ExpirationDateTime ?? expiration).UtcDateTime;

            if (!state.Subscriptions.Contains(current))
                state.Subscriptions.Add(current);

            changed = true;
        }

        var staleSubscriptions = state.Subscriptions
            .Where(s => !desiredResources.Contains(s.Resource, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var subscription in staleSubscriptions)
        {
            if (!string.IsNullOrWhiteSpace(subscription.SubscriptionId))
            {
                try
                {
                    await graphTodoClient.DeleteSubscriptionAsync(subscription.SubscriptionId, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to delete stale push subscription {SubscriptionId} for user {UserId}", subscription.SubscriptionId, userId);
                }
            }

            state.Subscriptions.Remove(subscription);
            changed = true;
        }

        return changed;
    }

    private async Task RunBackgroundRefreshAsync(
        string userId,
        IGraphTodoClient graphTodoClient,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var userTimeZoneService = scope.ServiceProvider.GetRequiredService<IUserTimeZoneService>();

        var graphTodoService = new GraphTodoService(
            graphTodoClient,
            userTimeZoneService,
            loggerFactory.CreateLogger<GraphTodoService>());

        var cachedTodoService = new CachedTodoService(
            graphTodoService,
            graphTodoClient,
            dbContextFactory,
            todoCacheOptions,
            userTimeZoneService,
            pushSyncGate,
            pushSyncHealthService,
            loggerFactory.CreateLogger<CachedTodoService>());

        await cachedTodoService.RunPushSyncAsync(userId, cancellationToken);
    }

    private static async Task<HashSet<string>> GetDesiredResourcesAsync(
        AppDbContext db,
        string userId,
        CancellationToken cancellationToken)
    {
        // Microsoft Graph change notifications for To Do are task-list scoped.
        // Task-list creation/removal still relies on the existing delta sync path.
        var resources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var listIds = await db.CachedTaskLists
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.IsSynced)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        foreach (var listId in listIds)
            resources.Add($"me/todo/lists/{listId}/tasks");

        return resources;
    }

    private static bool HasNotificationUrl(string? notificationUrl) =>
        Uri.TryCreate(notificationUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
