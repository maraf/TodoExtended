using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class SqliteDistributedCache(IDbContextFactory<AppDbContext> dbContextFactory) : IDistributedCache
{
    public byte[]? Get(string key)
    {
        return GetAsync(key).GetAwaiter().GetResult();
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync(token);
        
        var entry = await dbContext.Set<DistributedCacheEntry>()
            .FirstOrDefaultAsync(e => e.Key == key, token);

        if (entry == null)
            return null;

        var now = DateTimeOffset.UtcNow;

        // Check absolute expiration
        if (entry.AbsoluteExpiration.HasValue && entry.AbsoluteExpiration.Value <= now)
        {
            await RemoveAsync(key, token);
            return null;
        }

        // Check sliding expiration
        if (entry.SlidingExpirationInSeconds.HasValue && entry.LastAccessed.HasValue)
        {
            var expiresAt = entry.LastAccessed.Value.AddSeconds(entry.SlidingExpirationInSeconds.Value);
            if (expiresAt <= now)
            {
                await RemoveAsync(key, token);
                return null;
            }

            // Update last accessed time for sliding expiration
            entry.LastAccessed = now;
            await dbContext.SaveChangesAsync(token);
        }

        return entry.Value;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        SetAsync(key, value, options).GetAwaiter().GetResult();
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync(token);
        
        var now = DateTimeOffset.UtcNow;
        var entry = await dbContext.Set<DistributedCacheEntry>()
            .FirstOrDefaultAsync(e => e.Key == key, token);

        if (entry == null)
        {
            entry = new DistributedCacheEntry
            {
                Key = key,
                Value = value,
                AbsoluteExpiration = options.AbsoluteExpiration,
                SlidingExpirationInSeconds = options.SlidingExpiration?.TotalSeconds,
                LastAccessed = now
            };
            dbContext.Set<DistributedCacheEntry>().Add(entry);
        }
        else
        {
            entry.Value = value;
            entry.AbsoluteExpiration = options.AbsoluteExpiration;
            entry.SlidingExpirationInSeconds = options.SlidingExpiration?.TotalSeconds;
            entry.LastAccessed = now;
        }

        await dbContext.SaveChangesAsync(token);
    }

    public void Refresh(string key)
    {
        RefreshAsync(key).GetAwaiter().GetResult();
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync(token);
        
        var entry = await dbContext.Set<DistributedCacheEntry>()
            .FirstOrDefaultAsync(e => e.Key == key, token);

        if (entry != null && entry.SlidingExpirationInSeconds.HasValue)
        {
            entry.LastAccessed = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(token);
        }
    }

    public void Remove(string key)
    {
        RemoveAsync(key).GetAwaiter().GetResult();
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync(token);
        
        var entry = await dbContext.Set<DistributedCacheEntry>()
            .FirstOrDefaultAsync(e => e.Key == key, token);

        if (entry != null)
        {
            dbContext.Set<DistributedCacheEntry>().Remove(entry);
            await dbContext.SaveChangesAsync(token);
        }
    }
}
