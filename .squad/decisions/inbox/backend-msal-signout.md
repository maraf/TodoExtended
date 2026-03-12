### MsalServiceException Handling — Sign Out on Irrecoverable Auth Failures

**Date:** 2026-03-13  
**Author:** Backend  
**Status:** Implemented

When MSAL token acquisition fails with an irrecoverable `MsalServiceException` (e.g. `invalid_client`, expired secrets, revoked consent, 401 status), the user is now signed out and redirected to the landing page — instead of being left on a broken page with console warnings.

**Key Decisions:**

1. **Two-tier auth error handling in Blazor pages:**
   - `MicrosoftIdentityWebChallengeUserException` → redirect to `MicrosoftIdentity/Account/SignIn` (re-consent, existing behavior)
   - `MsalServiceException` (irrecoverable) → redirect to `MicrosoftIdentity/Account/SignOut` (clear broken session)
   - MSAL catch is evaluated first via exception filter ordering

2. **Helper: `AuthExceptionHelper.IsIrrecoverableMsalError(Exception)`** — Walks the full exception chain (including `AggregateException` inner exceptions) checking for `MsalServiceException` with `ErrorCode == "invalid_client"` or `StatusCode == 401`. This handles cases where MSAL errors are wrapped by Graph SDK or other middleware.

3. **CachedTodoService: explicit `MsalServiceException` catches** — Added before existing `ObjectDisposedException` and generic catches in all sync methods. These log at Warning level and re-throw (not swallow), so the error propagates to the Blazor page for sign-out redirect. This prevents the `ShouldRebuildCache` logic from running on auth failures (which would just fail again).

4. **Demo mode unaffected** — Demo mode doesn't use MSAL, so `MsalServiceException` is never thrown. The new catch blocks are inert in demo mode.

**Files Changed:**
- `Services/AuthExceptionHelper.cs` (new) — Static helper for MSAL error detection
- `Services/CachedTodoService.cs` — 8 new `MsalServiceException` catch blocks
- 8 Razor files — 19 new MSAL catch blocks (NavMenu, Tasks, Today, Home, Templates, ApiKeys, SyncSettings, TaskStatusCheckbox)

**Impact:** No interface changes. Build clean with `-warnaserror`. All 75 tests pass (21 unit + 54 component).
