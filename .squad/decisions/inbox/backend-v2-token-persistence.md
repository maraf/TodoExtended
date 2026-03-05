# Decision: V2 Persistent MSAL Token Cache for API Key Graph Calls

**Date:** March 5, 2026  
**Status:** Implemented  
**Team:** Backend  

## Context

In V1, MSAL token cache was in-memory (`AddInMemoryTokenCaches()`). When users authenticated via OIDC, their access + refresh tokens were stored in memory. However, API key-authenticated requests couldn't call MS Graph because tokens weren't available outside the OIDC session scope.

## Decision

Persist MSAL's token cache to SQLite using `IDistributedCache` abstraction. This enables:

1. OIDC sign-ins to automatically persist tokens to DB
2. API key requests to load persisted tokens and use MSAL to silently acquire Graph access tokens (via stored refresh tokens)
3. Continued normal operation of existing OIDC Blazor flow

## Implementation

### Core Components

1. **SqliteDistributedCache** (`Services/SqliteDistributedCache.cs`)
   - Implements `IDistributedCache` backed by `DistributedCacheEntry` table
   - Supports absolute and sliding expiration
   - Uses `IDbContextFactory<AppDbContext>` to avoid singleton/scoped conflicts
   - Operations: Get/Set/Refresh/Remove with expiration checking

2. **DistributedCacheEntry** (`Data/DistributedCacheEntry.cs`)
   - Entity: Key (PK, 512 chars), Value (BLOB), AbsoluteExpiration, SlidingExpirationInSeconds, LastAccessed
   - Indexes on AbsoluteExpiration and LastAccessed for cleanup queries

3. **ApiKeyGraphClientFactory** (`Services/ApiKeyGraphClientFactory.cs`)
   - Creates `GraphServiceClient` for API key-authenticated users
   - Loads user's `HomeAccountId` from database
   - Builds `ConfidentialClientApplication` with Azure AD config
   - Attaches distributed cache via MSAL event hooks (`SetBeforeAccessAsync`, `SetAfterAccessAsync`)
   - Calls `AcquireTokenSilent` with cached account
   - Handles `MsalUiRequiredException` with clear error message

4. **User Entity Enhancement** (`Data/User.cs`)
   - Added `HomeAccountId` property (nullable string, 256 chars)
   - Stores MSAL cache key in `{oid}.{tid}` format
   - Captured during OIDC sign-in via `UserSyncMiddleware`

5. **UserSyncMiddleware Enhancement**
   - Extracts `tid` claim from OIDC tokens
   - Computes `homeAccountId = $"{oid}.{tid}"`
   - Stores in User entity for cache key lookup

6. **GraphServiceClient Registration Override** (`Program.cs`)
   - Factory checks if request is API key authenticated
   - If API key: uses `ApiKeyGraphClientFactory.CreateForUser(userId)`
   - If OIDC: uses `OidcTokenProvider` wrapped in `BaseBearerTokenAuthenticationProvider`
   - Replaces default registration from `AddMicrosoftGraph()`

7. **SimpleDbContextFactory** (`Data/SimpleDbContextFactory.cs`)
   - Custom `IDbContextFactory<AppDbContext>` implementation
   - Manually constructs `DbContextOptions` per call
   - Registered as singleton to serve singleton services

### Configuration Changes

**Program.cs**
```csharp
// Before
.AddInMemoryTokenCaches();

// After
.AddDistributedTokenCaches();

// Register custom distributed cache
builder.Services.AddSingleton<IDistributedCache, SqliteDistributedCache>();
```

### Migration

**AddPersistentTokenCache**
- Creates `DistributedCacheEntries` table with Key (PK), Value, expiration fields
- Adds `HomeAccountId` column to Users table
- Indexes on AbsoluteExpiration and LastAccessed

## Rationale

### Why SQLite-backed IDistributedCache?

- **MSAL Integration**: Microsoft.Identity.Web's `AddDistributedTokenCaches()` automatically uses registered `IDistributedCache`
- **No External Dependencies**: Reuses existing SQLite database
- **Simplicity**: Single implementation serves both MSAL and potential future caching needs
- **Performance**: Local SQLite adequate for single-user app

### Why Manual ConfidentialClientApplication for API Keys?

- **Isolation**: API key flow doesn't have HttpContext claims that `ITokenAcquisition` expects
- **Explicit Control**: Clear cache loading/saving logic via MSAL event hooks
- **Error Handling**: Can catch and handle `MsalUiRequiredException` with specific messaging

### Why HomeAccountId Storage?

- MSAL cache keys are based on `homeAccountId` (`{oid}.{tid}`)
- For "consumers" tenant, `tid` varies per user
- Storing during OIDC sign-in avoids complex cache key enumeration

## Alternatives Considered

1. **Use ITokenAcquisition for both flows**
   - Requires constructing ClaimsPrincipal with correct claims (oid, tid) for API key users
   - More complex claim manipulation
   - Less explicit error handling

2. **Store tokens in UserToken entity**
   - More manual token refresh logic
   - Reinventing MSAL's token management
   - Less secure (manual encryption/decryption)

3. **Redis/SQL Server for distributed cache**
   - Overkill for single-user local app
   - Adds external dependency

## Security Considerations

- MSAL tokens stored encrypted by MSAL itself (binary serialization)
- SQLite database should be protected with filesystem permissions
- Refresh tokens have 90-day sliding expiration (MSAL default)
- On token expiration, user must re-authenticate via OIDC

## Testing Notes

To test API key Graph access:
1. Sign in via OIDC to persist tokens
2. Create API key via `/api/keys/create`
3. Make API request with `Authorization: Bearer {apiKey}` header
4. Verify Graph calls succeed using cached tokens
5. After 90 days or explicit revoke, verify graceful error handling

## Future Enhancements

- Background token refresh job to keep tokens alive
- Admin endpoint to view/revoke cached tokens
- Telemetry for token acquisition success/failure rates

## Files Changed

- `Data/User.cs` - Added HomeAccountId
- `Data/AppDbContext.cs` - Added DistributedCacheEntry DbSet + config
- `Middleware/UserSyncMiddleware.cs` - Capture tid + homeAccountId
- `Program.cs` - Replace in-memory cache, register factory, override GraphServiceClient

## Files Created

- `Data/DistributedCacheEntry.cs`
- `Data/SimpleDbContextFactory.cs`
- `Services/SqliteDistributedCache.cs`
- `Services/ApiKeyGraphClientFactory.cs`
- `Services/OidcTokenProvider.cs`
- `Migrations/20260305223206_AddPersistentTokenCache.cs`
