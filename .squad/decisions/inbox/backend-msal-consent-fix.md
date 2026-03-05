# Decision: MSAL Consent Exception Handling in Blazor Server Pages

**By:** Backend  
**Date:** 2025-07-18  
**Status:** Implemented

## Context

Blazor Interactive Server pages communicate via SignalR, which cannot perform HTTP redirects. When Microsoft Graph API tokens expire or need user consent, Microsoft.Identity.Web throws `MicrosoftIdentityWebChallengeUserException` (IDW10502). Without handling, this surfaces as a generic error message.

## Decision

All Blazor pages that call `ITodoService` methods (which hit Graph API) now catch `MicrosoftIdentityWebChallengeUserException` before the generic `Exception` handler and redirect to `MicrosoftIdentity/Account/SignIn` with `forceLoad: true`.

## Pattern

```csharp
catch (Exception ex) when (ex is MicrosoftIdentityWebChallengeUserException)
{
    NavigationManager.NavigateTo("MicrosoftIdentity/Account/SignIn", forceLoad: true);
    return;
}
```

## Impact

- Any new Blazor page calling `ITodoService` (or any downstream API via Microsoft.Identity.Web) must follow this same pattern.
- `forceLoad: true` is required to escape the SignalR circuit.
