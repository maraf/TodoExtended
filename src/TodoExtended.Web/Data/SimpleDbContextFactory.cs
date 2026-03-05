using Microsoft.EntityFrameworkCore;

namespace TodoExtended.Web.Data;

public class SimpleDbContextFactory(string connectionString) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}
