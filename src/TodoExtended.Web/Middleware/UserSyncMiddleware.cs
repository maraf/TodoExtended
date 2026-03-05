using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Middleware;

public class UserSyncMiddleware(RequestDelegate next, ILogger<UserSyncMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Only sync for OIDC authenticated users, not API key users
            var isApiKeyAuth = context.User.HasClaim(c => c.Type == "apikey" && c.Value == "true");
            var isOidcAuth = context.User.Identity.AuthenticationType == OpenIdConnectDefaults.AuthenticationScheme;

            if (isOidcAuth && !isApiKeyAuth)
            {
                var oid = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
                var tid = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
                var email = context.User.FindFirst(ClaimTypes.Email)?.Value 
                    ?? context.User.FindFirst("preferred_username")?.Value 
                    ?? "unknown@unknown.com";
                var displayName = context.User.FindFirst(ClaimTypes.Name)?.Value 
                    ?? context.User.FindFirst("name")?.Value 
                    ?? "Unknown User";

                if (!string.IsNullOrEmpty(oid))
                {
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == oid);
                    var now = DateTime.UtcNow;
                    
                    // Compute home account ID (oid.tid format) for MSAL cache key
                    var homeAccountId = !string.IsNullOrEmpty(tid) ? $"{oid}.{tid}" : oid;

                    if (user == null)
                    {
                        user = new User
                        {
                            Id = oid,
                            Email = email,
                            DisplayName = displayName,
                            HomeAccountId = homeAccountId,
                            CreatedUtc = now,
                            LastSeenUtc = now
                        };
                        dbContext.Users.Add(user);
                        logger.LogInformation("Created new user record for {Email} (OID: {Oid}, HomeAccountId: {HomeAccountId})", 
                            email, oid, homeAccountId);
                    }
                    else
                    {
                        user.Email = email;
                        user.DisplayName = displayName;
                        user.HomeAccountId = homeAccountId;
                        user.LastSeenUtc = now;
                    }

                    await dbContext.SaveChangesAsync();
                }
            }
        }

        await next(context);
    }
}
