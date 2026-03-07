using Microsoft.EntityFrameworkCore;

namespace TodoExtended.Web.Data;

public class SimpleDbContextFactory(string connectionString, EnableForeignKeysInterceptor foreignKeysInterceptor) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(connectionString)
                      .AddInterceptors(foreignKeysInterceptor);
        return new AppDbContext(optionsBuilder.Options);
    }

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}
