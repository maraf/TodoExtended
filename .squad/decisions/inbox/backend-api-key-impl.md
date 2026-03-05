# API Key Authentication Implementation

**Date:** 2025-01-XX  
**Status:** Implemented  
**Team:** Backend

## Context

Users need programmatic access to the TodoExtended API for automation and integrations. The existing OIDC authentication requires browser-based flows which aren't suitable for scripts or third-party tools.

## Decision

Implemented API key authentication alongside existing OIDC authentication with the following design:

### V1 Scope (Implemented)
- **Dual authentication schemes**: Both OIDC and API key schemes registered, authorization policy accepts either
- **In-memory token caching**: Kept existing `AddInMemoryTokenCaches()` for simplicity
- **Session dependency**: API key requests work only while user's OIDC session is active on server (tokens in MSAL in-memory cache)
- **Key format**: `tek_` prefix + 43 chars base64url (32 random bytes)
- **Storage**: SHA256 hash (lowercase hex) stored in database, plain key returned only once at creation
- **User sync**: Middleware auto-creates User records on OIDC sign-in, extracts OID/email/displayName from claims
- **REST API**: Minimal APIs at `/api` endpoints (templates, today's tasks, key management) secured with `RequireAuthorization()`

### Out of Scope for V1
- **Persistent token storage**: User tokens not saved to database (UserToken entity exists but unused)
- **Token refresh**: No automatic token refresh for API key users after server restart
- **Distributed cache**: Not implemented yet (would enable multi-server scenarios and token persistence)

## Consequences

### Positive
- Simple implementation, low complexity
- Works well for single-server deployment
- Clear security model (hash-based validation)
- Proper separation of concerns (handler, middleware, service)

### Negative
- API keys stop working after server restart (user must sign in via browser again)
- Not suitable for true "headless" scenarios where user never signs in interactively
- Single-server limitation (in-memory cache not shared across instances)

### Neutral
- Sets up infrastructure for V2 distributed cache migration
- UserToken entity ready for future token persistence

## V2 Roadmap (Not Implemented)
1. Replace `AddInMemoryTokenCaches()` with `AddDistributedTokenCaches()` backed by SQLite
2. Use `ITokenAcquisition` with stored tokens for API key requests
3. Implement token refresh logic for long-lived API key sessions
4. Consider token encryption for UserToken.EncryptedCacheData

## Files Modified
- `Data/User.cs`, `Data/ApiKey.cs`, `Data/UserToken.cs` (new entities)
- `Data/AppDbContext.cs` (DbSets + entity configs)
- `Authentication/ApiKeyAuthenticationHandler.cs` (new)
- `Middleware/UserSyncMiddleware.cs` (new)
- `Services/IApiKeyService.cs`, `Services/ApiKeyService.cs` (new)
- `Program.cs` (auth setup, middleware, API endpoints)
- Migration: `AddApiKeySupport`
