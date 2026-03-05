using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class ApiKeyGraphClientFactory(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IDistributedCache distributedCache,
    IConfiguration configuration,
    ILogger<ApiKeyGraphClientFactory> logger)
{
    public GraphServiceClient CreateForUser(string userId)
    {
        var authProvider = new ApiKeyGraphAuthProvider(
            userId,
            dbContextFactory,
            distributedCache,
            configuration,
            logger);
        
        return new GraphServiceClient(authProvider);
    }

    private class ApiKeyGraphAuthProvider(
        string userId,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IDistributedCache distributedCache,
        IConfiguration configuration,
        ILogger logger) : IAuthenticationProvider
    {
        public async Task AuthenticateRequestAsync(
            Microsoft.Kiota.Abstractions.RequestInformation request,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            var accessToken = await GetAccessTokenAsync(cancellationToken);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            // Get user's home account ID from database
            using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            
            if (user == null || string.IsNullOrEmpty(user.HomeAccountId))
            {
                throw new InvalidOperationException($"User {userId} not found or has no cached tokens. User must sign in via OIDC first.");
            }

            // Build MSAL confidential client app
            var azureAdConfig = configuration.GetSection("AzureAd");
            var clientId = azureAdConfig["ClientId"]!;
            var clientSecret = azureAdConfig["ClientSecret"]!;
            var tenantId = azureAdConfig["TenantId"]!;
            var instance = azureAdConfig["Instance"]!;
            
            var authority = $"{instance}{tenantId}/";
            
            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(authority)
                .Build();

            // Hook into MSAL's token cache with our distributed cache.
            // Microsoft.Identity.Web stores user tokens under the homeAccountId key.
            var homeAccountId = user.HomeAccountId!;
            
            app.UserTokenCache.SetBeforeAccessAsync(async args =>
            {
                var cachedData = await distributedCache.GetAsync(homeAccountId, cancellationToken);
                if (cachedData != null && cachedData.Length > 0)
                {
                    args.TokenCache.DeserializeMsalV3(cachedData);
                    logger.LogDebug("Loaded {Bytes} bytes of MSAL cache for user {UserId}", cachedData.Length, userId);
                }
                else
                {
                    logger.LogWarning("No MSAL cache found in distributed cache for key {Key}", homeAccountId);
                }
            });

            app.UserTokenCache.SetAfterAccessAsync(async args =>
            {
                if (args.HasStateChanged)
                {
                    var serializedData = args.TokenCache.SerializeMsalV3();
                    var key = args.SuggestedCacheKey ?? homeAccountId;
                    await distributedCache.SetAsync(
                        key,
                        serializedData,
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(90)
                        },
                        cancellationToken);
                    logger.LogDebug("Persisted {Bytes} bytes of refreshed MSAL cache for user {UserId}", serializedData.Length, userId);
                }
            });

            // Get the cached account
#pragma warning disable CS0618 // Using GetAccountsAsync for compatibility
            var accounts = await app.GetAccountsAsync();
#pragma warning restore CS0618
            var account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == user.HomeAccountId);

            if (account == null)
            {
                throw new InvalidOperationException($"No cached account found for user {userId}. User must sign in via OIDC first.");
            }

            // Acquire token silently using cached refresh token
            var scopes = configuration.GetSection("Graph:Scopes").Get<string[]>()!;
            
            try
            {
                var result = await app.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync(cancellationToken);
                
                logger.LogDebug("Successfully acquired Graph token for user {UserId} via API key flow", userId);
                return result.AccessToken;
            }
            catch (MsalUiRequiredException ex)
            {
                logger.LogWarning(ex, "Token acquisition failed for user {UserId} - user must sign in again via OIDC", userId);
                throw new InvalidOperationException("Cached tokens expired. User must sign in via OIDC again.", ex);
            }
        }
    }
}
