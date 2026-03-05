using Microsoft.EntityFrameworkCore;

namespace TodoExtended.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.TaskListId).HasMaxLength(256);
            entity.Property(e => e.TaskListName).HasMaxLength(256);
        });
    }
}
