using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using TodoExtended.Web.Data;
using TodoExtended.Web.Services;

namespace TodoExtended.Tests.Services;

/// <summary>
/// Tests for <see cref="SqliteDistributedCache"/> focusing on expiration behaviour.
/// Each test gets its own temporary SQLite file so they remain independent.
/// </summary>
public class SqliteDistributedCacheTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IDbContextFactory<AppDbContext> _factory = null!;

    public SqliteDistributedCacheTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = $"Data Source={_dbPath}";
            _factory = new SimpleDbContextFactory(connectionString, new EnableForeignKeysInterceptor());

            using var seed = _factory.CreateDbContext();
            seed.Database.EnsureCreated();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    #region AbsoluteExpirationRelativeToNow

    [Fact]
    public async Task SetAsync_AbsoluteExpirationRelativeToNow_StoresAbsoluteExpiration()
    {
        var cache = new SqliteDistributedCache(_factory);

        var relativeExpiry = TimeSpan.FromDays(90);
        var before = DateTimeOffset.UtcNow;

        await cache.SetAsync("key1", [1, 2, 3], new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = relativeExpiry
        });

        var after = DateTimeOffset.UtcNow;

        using var db = _factory.CreateDbContext();
        var entry = await db.Set<DistributedCacheEntry>().FirstAsync(e => e.Key == "key1");

        Assert.NotNull(entry.AbsoluteExpiration);
        Assert.InRange(entry.AbsoluteExpiration!.Value,
            before + relativeExpiry,
            after + relativeExpiry);
    }

    [Fact]
    public async Task GetAsync_AbsoluteExpirationRelativeToNow_ReturnsValueBeforeExpiry()
    {
        var cache = new SqliteDistributedCache(_factory);

        await cache.SetAsync("key2", [42], new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
        });

        var result = await cache.GetAsync("key2");

        Assert.NotNull(result);
        Assert.Equal([42], result);
    }

    [Fact]
    public async Task GetAsync_EntryWithExpiredAbsoluteExpiration_ReturnsNull()
    {
        // Write an already-expired entry directly via the DB factory to bypass
        // the DistributedCacheEntryOptions validation (which rejects past values).
        using (var db = _factory.CreateDbContext())
        {
            db.Set<DistributedCacheEntry>().Add(new DistributedCacheEntry
            {
                Key = "key3",
                Value = [99],
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(-1),
                LastAccessed = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var cache = new SqliteDistributedCache(_factory);
        var result = await cache.GetAsync("key3");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_UpdateExisting_AbsoluteExpirationRelativeToNow_UpdatesExpiration()
    {
        var cache = new SqliteDistributedCache(_factory);

        // First write — no expiry.
        await cache.SetAsync("key4", [1], new DistributedCacheEntryOptions());

        // Second write — set a relative expiry.
        var relativeExpiry = TimeSpan.FromDays(30);
        var before = DateTimeOffset.UtcNow;
        await cache.SetAsync("key4", [2], new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = relativeExpiry
        });
        var after = DateTimeOffset.UtcNow;

        using var db = _factory.CreateDbContext();
        var entry = await db.Set<DistributedCacheEntry>().FirstAsync(e => e.Key == "key4");

        Assert.NotNull(entry.AbsoluteExpiration);
        Assert.InRange(entry.AbsoluteExpiration!.Value,
            before + relativeExpiry,
            after + relativeExpiry);
        Assert.Equal([2], entry.Value);
    }

    [Fact]
    public async Task SetAsync_AbsoluteExpiration_TakesPrecedenceOverRelative()
    {
        var cache = new SqliteDistributedCache(_factory);

        var absolute = DateTimeOffset.UtcNow.AddDays(7);

        await cache.SetAsync("key5", [7], new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = absolute,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(90)
        });

        using var db = _factory.CreateDbContext();
        var entry = await db.Set<DistributedCacheEntry>().FirstAsync(e => e.Key == "key5");

        // The explicit absolute value should win.
        Assert.Equal(absolute, entry.AbsoluteExpiration!.Value, precision: TimeSpan.FromSeconds(1));
    }

    #endregion
}
