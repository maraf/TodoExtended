using Microsoft.EntityFrameworkCore;

namespace TodoExtended.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();
    public DbSet<CachedTaskList> CachedTaskLists => Set<CachedTaskList>();
    public DbSet<CachedTask> CachedTasks => Set<CachedTask>();
    public DbSet<SyncMetadata> SyncMetadata => Set<SyncMetadata>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<DistributedCacheEntry> DistributedCacheEntries => Set<DistributedCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasConversion<string>();
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.TaskListId).HasMaxLength(256);
            entity.Property(e => e.TaskListName).HasMaxLength(256);
            entity.Property(e => e.UserId).HasMaxLength(256);
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CachedTaskList>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(512);
            entity.Property(e => e.DeltaToken).HasMaxLength(2048);
            entity.Property(e => e.UserId).HasMaxLength(256);
            entity.HasIndex(e => e.LastSyncUtc);
            entity.HasIndex(e => new { e.UserId, e.IsSynced });
        });

        modelBuilder.Entity<CachedTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.ListId).HasMaxLength(256);
            entity.Property(e => e.Title).HasMaxLength(512);
            entity.Property(e => e.Importance).HasMaxLength(32);
            entity.Property(e => e.Tags).HasMaxLength(1024);
            
            entity.Property(e => e.UserId).HasMaxLength(256);
            entity.HasIndex(e => e.ListId);
            entity.HasIndex(e => new { e.UserId, e.IsDeleted, e.DueDate });
            entity.HasIndex(e => new { e.ListId, e.IsDeleted });
            
            entity.HasOne(e => e.List)
                .WithMany(e => e.Tasks)
                .HasForeignKey(e => e.ListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncMetadata>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(256);
            entity.Property(e => e.Value).HasMaxLength(4096);
            entity.Property(e => e.UserId).HasMaxLength(256);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(256);
            entity.Property(e => e.HomeAccountId).HasMaxLength(256);
            entity.Property(e => e.PinnedTags).HasMaxLength(2048);
            entity.HasIndex(e => e.Email);
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.KeyHash).HasMaxLength(64);
            
            entity.HasIndex(e => e.KeyHash);
            entity.HasIndex(e => new { e.UserId, e.IsRevoked });
            
            entity.HasOne(e => e.User)
                .WithMany(e => e.ApiKeys)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserToken>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(256);
            
            entity.HasOne(e => e.User)
                .WithOne(e => e.Token)
                .HasForeignKey<UserToken>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DistributedCacheEntry>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(512);
            entity.HasIndex(e => e.AbsoluteExpiration);
            entity.HasIndex(e => e.LastAccessed);
        });
    }
}
