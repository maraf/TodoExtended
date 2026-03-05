using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string Scheme => DefaultScheme;
    public string HeaderName { get; set; } = "X-Api-Key";
}

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext dbContext)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var keyHash = ComputeHash(providedApiKey);

        var apiKey = await dbContext.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && !k.IsRevoked);

        if (apiKey?.User == null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        // Update last used timestamp
        apiKey.LastUsedUtc = DateTime.UtcNow;
        apiKey.User.LastSeenUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKey.User.Id),
            new Claim(ClaimTypes.Name, apiKey.User.DisplayName),
            new Claim(ClaimTypes.Email, apiKey.User.Email),
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", apiKey.User.Id),
            new Claim("apikey", "true")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
