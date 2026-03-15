using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace TodoExtended.Web.Services;

/// <summary>
/// Thrown when the user's identity claims are missing (e.g. the Blazor circuit outlived the auth session).
/// </summary>
public sealed class NotAuthenticatedException : InvalidOperationException
{
    public NotAuthenticatedException() : base("User not authenticated") { }
}

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

    /// <summary>
    /// Returns <c>true</c> when the exception indicates the user identity claims are missing
    /// (e.g. the Blazor circuit outlived the auth session).
    /// </summary>
    public static bool IsUnauthenticatedUser(Exception ex) =>
        ex is NotAuthenticatedException;

    /// <summary>
    /// Returns <c>true</c> when the exception (or any exception in its chain) is a
    /// <see cref="MsalUiRequiredException"/>, indicating that cached tokens have expired and
    /// the user must sign in again to obtain fresh tokens.
    /// This covers the API-key flow where <see cref="Services.ApiKeyGraphClientFactory"/> wraps
    /// the original <see cref="MsalUiRequiredException"/> inside an
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public static bool IsConsentRequired(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is MsalUiRequiredException)
                return true;
        }

        if (ex is AggregateException aggEx)
        {
            foreach (var inner in aggEx.InnerExceptions)
            {
                if (IsConsentRequired(inner))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> for any exception that requires an auth redirect
    /// (irrecoverable MSAL error, consent challenge, or missing user identity).
    /// Use as an exception filter so these exceptions bubble up to <see cref="AuthErrorBoundary"/>.
    /// </summary>
    public static bool IsAuthException(Exception ex) =>
        IsIrrecoverableMsalError(ex) ||
        IsConsentRequired(ex) ||
        IsUnauthenticatedUser(ex) ||
        ex is MicrosoftIdentityWebChallengeUserException;
}
