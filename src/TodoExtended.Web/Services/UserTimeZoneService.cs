using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;
using TodoExtended.Web.Extensions;

namespace TodoExtended.Web.Services;

public class UserTimeZoneService(
    IHttpContextAccessor httpContextAccessor,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<UserTimeZoneService> logger) : IUserTimeZoneService
{
    public async Task<TimeZoneInfo> GetCurrentUserTimeZoneAsync()
    {
        var oid = httpContextAccessor.HttpContext?.User.GetUserIdOrNull();

        if (!string.IsNullOrEmpty(oid))
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var timeZone = await db.Users
                .Where(u => u.Id == oid)
                .Select(u => u.TimeZone)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(timeZone))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
                }
                catch (TimeZoneNotFoundException ex)
                {
                    logger.LogWarning(ex, "Unknown timezone '{TimeZone}' for user {UserId}, falling back to UTC", timeZone, oid);
                }
            }
        }

        return TimeZoneInfo.Utc;
    }
}
