using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TodoExtended.Web.Data;
using TodoExtended.Web.Services;

namespace TodoExtended.Tests.Services;

public class PushSyncHealthServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SimpleDbContextFactory _factory;

    public PushSyncHealthServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-pushsync-health-{Guid.NewGuid():N}.db");
        _factory = new SimpleDbContextFactory($"Data Source={_dbPath}", new EnableForeignKeysInterceptor());

        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task IsHealthyAsync_WhenSubscriptionIsActiveAndRecentSuccessExists_ReturnsTrue()
    {
        const string userId = "user-healthy";
        var stateStore = new PushSyncStateStore(_factory, NullLogger<PushSyncStateStore>.Instance);
        var service = CreateService(stateStore, notificationUrl: "https://example.test/api/pushsync/webhook");

        await stateStore.SaveAsync(userId, new PushSyncState
        {
            UserId = userId,
            PendingRefresh = false,
            Subscriptions =
            [
                new PushSyncSubscriptionState
                {
                    Resource = "me/todo/lists/list-1/tasks",
                    SubscriptionId = "sub-1",
                    ClientState = PushSyncStateStore.CreateClientState(userId),
                    ExpiresUtc = DateTime.UtcNow.AddHours(1),
                }
            ]
        });
        await service.RecordSuccessAsync(userId);

        var isHealthy = await service.IsHealthyAsync(userId);

        Assert.True(isHealthy);
    }

    [Fact]
    public async Task IsHealthyAsync_WhenRefreshIsPending_ReturnsFalse()
    {
        const string userId = "user-pending";
        var stateStore = new PushSyncStateStore(_factory, NullLogger<PushSyncStateStore>.Instance);
        var service = CreateService(stateStore, notificationUrl: "https://example.test/api/pushsync/webhook");

        await stateStore.SaveAsync(userId, new PushSyncState
        {
            UserId = userId,
            PendingRefresh = true,
            Subscriptions =
            [
                new PushSyncSubscriptionState
                {
                    Resource = "me/todo/lists/list-1/tasks",
                    SubscriptionId = "sub-2",
                    ClientState = PushSyncStateStore.CreateClientState(userId),
                    ExpiresUtc = DateTime.UtcNow.AddHours(1),
                }
            ]
        });
        await service.RecordSuccessAsync(userId);
        await stateStore.SaveAsync(userId, new PushSyncState
        {
            UserId = userId,
            PendingRefresh = true,
            Subscriptions =
            [
                new PushSyncSubscriptionState
                {
                    Resource = "me/todo/lists/list-1/tasks",
                    SubscriptionId = "sub-2",
                    ClientState = PushSyncStateStore.CreateClientState(userId),
                    ExpiresUtc = DateTime.UtcNow.AddHours(1),
                }
            ]
        });

        var isHealthy = await service.IsHealthyAsync(userId);

        Assert.False(isHealthy);
    }

    private PushSyncHealthService CreateService(PushSyncStateStore stateStore, string? notificationUrl) =>
        new(
            _factory,
            Options.Create(new PushSyncOptions
            {
                Enabled = true,
                NotificationUrl = notificationUrl,
                HealthTtlMinutes = 10,
                RenewBeforeMinutes = 30,
            }),
            stateStore,
            NullLogger<PushSyncHealthService>.Instance);
}
