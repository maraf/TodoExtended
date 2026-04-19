using Microsoft.Extensions.Options;

namespace TodoExtended.Web.Services;

public class PushSyncGate(IOptions<PushSyncOptions> options) : IPushSyncGate
{
    private readonly PushSyncOptions _options = options.Value;
    private readonly HashSet<string> _normalizedAllowList = options.Value.AllowedUsers
        .Select(NormalizeIdentifier)
        .Where(value => !string.IsNullOrEmpty(value))
        .Select(value => value!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsEligible(string? userEmail, string? preferredUsername)
    {
        if (!_options.Enabled || _normalizedAllowList.Count == 0)
            return false;

        var normalizedEmail = NormalizeIdentifier(userEmail);
        var normalizedPreferredUsername = NormalizeIdentifier(preferredUsername);

        if (!string.IsNullOrEmpty(normalizedEmail) && _normalizedAllowList.Contains(normalizedEmail))
            return true;

        if (!string.IsNullOrEmpty(normalizedPreferredUsername) && _normalizedAllowList.Contains(normalizedPreferredUsername))
            return true;

        return false;
    }

    private static string? NormalizeIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
