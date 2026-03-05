using Microsoft.Identity.Web;
using Microsoft.Kiota.Abstractions.Authentication;

namespace TodoExtended.Web.Services;

public class OidcTokenProvider(ITokenAcquisition tokenAcquisition, string[] scopes) 
    : IAccessTokenProvider
{
    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri, 
        Dictionary<string, object>? additionalAuthenticationContext = null, 
        CancellationToken cancellationToken = default)
    {
        var token = await tokenAcquisition.GetAccessTokenForUserAsync(scopes);
        return token;
    }

    public AllowedHostsValidator AllowedHostsValidator => new();
}
