using System.Security.Claims;

namespace TodoExtended.Web.Extensions;

public static class ClaimsPrincipalExtensions
{
    private const string ObjectIdentifierClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>
    /// Gets the user ID from the OID claim (OIDC) or NameIdentifier claim (API key auth).
    /// Returns <c>null</c> if neither claim is present.
    /// </summary>
    public static string? GetUserIdOrNull(this ClaimsPrincipal principal) =>
        principal.FindFirst(ObjectIdentifierClaim)?.Value
        ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// Gets the user ID from claims, throwing if the user is not authenticated.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.GetUserIdOrNull()
        ?? throw new InvalidOperationException("User not authenticated");
}
