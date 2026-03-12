using Microsoft.Identity.Client;

namespace TodoExtended.Web.Services;

/// <summary>
/// Identifies irrecoverable MSAL authentication failures (e.g. invalid_client, expired secrets)
/// that require signing the user out rather than just re-authenticating.
/// </summary>
public static class AuthExceptionHelper
{
    public static bool IsIrrecoverableMsalError(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is MsalServiceException msalEx &&
                (string.Equals(msalEx.ErrorCode, "invalid_client", StringComparison.OrdinalIgnoreCase) ||
                 msalEx.StatusCode == 401))
            {
                return true;
            }
        }

        if (ex is AggregateException aggEx)
        {
            foreach (var inner in aggEx.InnerExceptions)
            {
                if (IsIrrecoverableMsalError(inner))
                    return true;
            }
        }

        return false;
    }
}
