namespace TodoExtended.Web.Services;

public record ApiKeyDto(int Id, string Name, DateTime CreatedUtc, DateTime? LastUsedUtc);
public record CreateApiKeyResult(int KeyId, string PlainKey);

public interface IApiKeyService
{
    Task<CreateApiKeyResult> CreateKeyAsync(string userId, string name);
    Task<IReadOnlyList<ApiKeyDto>> GetUserKeysAsync(string userId);
    Task RevokeKeyAsync(int keyId, string userId);
}
