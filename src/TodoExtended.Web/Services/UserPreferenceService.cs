using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class UserPreferenceService(IDbContextFactory<AppDbContext> contextFactory) : IUserPreferenceService
{
    public async Task<bool> GetIsDarkModeAsync(string userId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        return user?.IsDarkMode ?? false;
    }

    public async Task SetIsDarkModeAsync(string userId, bool isDarkMode)
    {
        await using var db = await contextFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user is not null)
        {
            user.IsDarkMode = isDarkMode;
            await db.SaveChangesAsync();
        }
    }
}
