using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TodoExtended.Web.Data;

namespace TodoExtended.Web.Services;

public class ApiKeyService(AppDbContext dbContext, ILogger<ApiKeyService> logger) : IApiKeyService
{
    public async Task<CreateApiKeyResult> CreateKeyAsync(string userId, string name)
    {
        var plainKey = GenerateApiKey();
        var keyHash = ComputeHash(plainKey);

        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = name,
            KeyHash = keyHash,
            CreatedUtc = DateTime.UtcNow,
            IsRevoked = false
        };

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Created API key '{Name}' (ID: {Id}) for user {UserId}", name, apiKey.Id, userId);

        return new CreateApiKeyResult(apiKey.Id, plainKey);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> GetUserKeysAsync(string userId)
    {
        return await dbContext.ApiKeys
            .Where(k => k.UserId == userId && !k.IsRevoked)
            .OrderByDescending(k => k.CreatedUtc)
            .Select(k => new ApiKeyDto(k.Id, k.Name, k.CreatedUtc, k.LastUsedUtc))
            .ToListAsync();
    }

    public async Task RevokeKeyAsync(int keyId, string userId)
    {
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId);

        if (apiKey == null)
        {
            throw new InvalidOperationException("API key not found or does not belong to user");
        }

        apiKey.IsRevoked = true;
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Revoked API key {KeyId} for user {UserId}", keyId, userId);
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return $"tek_{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
