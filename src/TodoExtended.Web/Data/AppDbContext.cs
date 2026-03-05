using Microsoft.EntityFrameworkCore;

namespace TodoExtended.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();
    public DbSet<CachedTaskList> CachedTaskLists => Set<CachedTaskList>();
    public DbSet<CachedTask> CachedTasks => Set<CachedTask>();
    public DbSet<SyncMetadata> SyncMetadata => Set<SyncMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.TaskListId).HasMaxLength(256);
            entity.Property(e => e.TaskListName).HasMaxLength(256);
        });

        modelBuilder.Entity<CachedTaskList>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(512);
            entity.Property(e => e.DeltaToken).HasMaxLength(2048);
            entity.HasIndex(e => e.LastSyncUtc);
        });

        modelBuilder.Entity<CachedTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.ListId).HasMaxLength(256);
            entity.Property(e => e.Title).HasMaxLength(512);
            entity.Property(e => e.Importance).HasMaxLength(32);
            
            entity.HasIndex(e => e.ListId);
            entity.HasIndex(e => new { e.IsDeleted, e.DueDate });
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
        });
    }
}
