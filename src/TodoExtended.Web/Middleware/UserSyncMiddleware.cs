using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Kiota.Abstractions.Authentication;
using TodoExtended.Web.Data;
using TodoExtended.Web.Services;

namespace TodoExtended.Web.Middleware;

public class UserSyncMiddleware(RequestDelegate next, ILogger<UserSyncMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Sync for OIDC/cookie-authenticated users, not API key or demo users.
            // After initial OIDC sign-in, subsequent requests use cookie auth,
            // so we check for the OID claim rather than AuthenticationType.
            var isApiKeyAuth = context.User.HasClaim(c => c.Type == "apikey" && c.Value == "true");
            var isDemoUser = context.User.HasClaim(c => c.Type == "demo" && c.Value == "true");

            if (!isApiKeyAuth && !isDemoUser)
            {
                var oid = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
                var tid = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
                var preferredUsername = NormalizeIdentifier(context.User.FindFirst("preferred_username")?.Value);
                var email = NormalizeIdentifier(context.User.FindFirst(ClaimTypes.Email)?.Value)
                    ?? preferredUsername
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

                    if (!string.IsNullOrWhiteSpace(preferredUsername))
                    {
                        var key = PushSyncMetadataKeys.PreferredUsername(user.Id);
                        var entry = await dbContext.SyncMetadata.FindAsync(key);
                        if (entry == null)
                        {
                            dbContext.SyncMetadata.Add(new SyncMetadata
                            {
                                Key = key,
                                Value = preferredUsername,
                                UpdatedUtc = now,
                                UserId = user.Id,
                            });
                        }
                        else if (!string.Equals(entry.Value, preferredUsername, StringComparison.Ordinal))
                        {
                            entry.Value = preferredUsername;
                            entry.UpdatedUtc = now;
                            entry.UserId = user.Id;
                        }
                    }

                    // ITokenAcquisition is only available when OIDC auth is registered (non-demo mode).
                    var tokenAcquisition = context.RequestServices.GetService<ITokenAcquisition>();
                    if (tokenAcquisition != null && string.IsNullOrEmpty(user.TimeZone))
                    {
                        try
                        {
                            // Use a separate token request with MailboxSettings.Read scope
                            // so it doesn't break regular Graph calls for users who haven't
                            // consented to this scope yet.
                            var authProvider = new BaseBearerTokenAuthenticationProvider(
                                new OidcTokenProvider(tokenAcquisition, ["MailboxSettings.Read"]));
                            var mailboxClient = new GraphServiceClient(authProvider);
                            var settings = await mailboxClient.Me.MailboxSettings.GetAsync();
                            if (!string.IsNullOrEmpty(settings?.TimeZone))
                            {
                                user.TimeZone = settings.TimeZone;
                                logger.LogInformation("Fetched timezone '{TimeZone}' for user {Email}", settings.TimeZone, email);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to fetch mailbox settings for user {Email}. User may need to sign out and sign back in to grant MailboxSettings.Read consent.", email);
                        }
                    }

                    await dbContext.SaveChangesAsync();
                }
            }
        }

        await next(context);
    }

    private static string? NormalizeIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
