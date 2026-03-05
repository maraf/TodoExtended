# API Key Authentication & Token Storage Design

**Author:** Architect  
**Date:** 2026-03-06  
**Status:** Proposed

## Overview

This design adds API key authentication to TodoExtended, enabling users to create named API keys that authenticate API requests without browser sign-in. Each key is tied to a user and their stored MS Graph tokens, allowing API-authenticated requests to call Graph on behalf of the user.

## Requirements

1. **API Keys per User** — Users can create/manage named API keys stored in SQLite
2. **Token Persistence** — Capture and persist OIDC/Graph access + refresh tokens per user
3. **API Endpoints** — Three endpoints authenticated via API key:
   - `GET /api/templates` — list user's templates
   - `POST /api/templates/{id}/execute` — create task from template
   - `GET /api/today` — get today's task list
4. **Security** — Hash API keys; encrypt tokens at rest; leverage existing MSAL infrastructure

## 1. Data Model

### 1.1 New Entities

#### ApiKey
Stores user API keys with one-way hash for secure comparison.

```csharp
namespace TodoExtended.Web.Data;

public class ApiKey
{
    public int Id { get; set; }
    public required string UserId { get; set; }  // Entra ID user OID
    public required string Name { get; set; }    // User-friendly name
    public required string KeyHash { get; set; } // SHA256 hash of the key
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public bool IsRevoked { get; set; }
    
    public User? User { get; set; }
}
```

#### User
Represents an authenticated user; stores their Entra ID object identifier.

```csharp
namespace TodoExtended.Web.Data;

public class User
{
    public required string Id { get; set; }      // Entra ID OID (primary key)
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
    public UserToken? Token { get; set; }
}
```

#### UserToken
Stores encrypted MSAL token cache data per user. MSAL's token cache is serialized as JSON and encrypted at rest.

```csharp
namespace TodoExtended.Web.Data;

public class UserToken
{
    public required string UserId { get; set; }  // FK to User (1:1)
    public required string EncryptedCacheData { get; set; }  // Encrypted MSAL token cache JSON
    public DateTime UpdatedUtc { get; set; }
    
    public User? User { get; set; }
}
```

### 1.2 EF Core Configuration

**AppDbContext changes:**

```csharp
public DbSet<User> Users => Set<User>();
public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
public DbSet<UserToken> UserTokens => Set<UserToken>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ... existing configurations ...
    
    modelBuilder.Entity<User>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasMaxLength(128);
        entity.Property(e => e.Email).HasMaxLength(256);
        entity.Property(e => e.DisplayName).HasMaxLength(256);
        entity.HasIndex(e => e.Email);
    });
    
    modelBuilder.Entity<ApiKey>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UserId).HasMaxLength(128);
        entity.Property(e => e.Name).HasMaxLength(128);
        entity.Property(e => e.KeyHash).HasMaxLength(64);  // SHA256 hex = 64 chars
        
        entity.HasIndex(e => e.UserId);
        entity.HasIndex(e => new { e.UserId, e.Name }).IsUnique();
        
        entity.HasOne(e => e.User)
            .WithMany(u => u.ApiKeys)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    });
    
    modelBuilder.Entity<UserToken>(entity =>
    {
        entity.HasKey(e => e.UserId);
        entity.Property(e => e.UserId).HasMaxLength(128);
        
        entity.HasOne(e => e.User)
            .WithOne(u => u.Token)
            .HasForeignKey<UserToken>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    });
}
```

## 2. Token Capture & Persistence Strategy

### 2.1 Approach: Distributed Token Cache with Database Backend

**Key insight:** Microsoft.Identity.Web uses MSAL's `ITokenCache` under the hood. By default, `.AddInMemoryTokenCaches()` is ephemeral. We'll replace this with a **custom distributed token cache** backed by our `UserToken` table.

**Pattern:** Implement `ITokenCacheSerializer` to:
- **Serialize:** Encrypt MSAL's cache blob and store in `UserToken.EncryptedCacheData`
- **Deserialize:** Decrypt and load cache blob from DB

**Why this works:**
- MSAL handles all token lifecycle (refresh, expiry, acquisition)
- No manual token parsing or storage
- Works seamlessly for both interactive (OIDC) and API key flows
- Existing `GraphServiceClient` injection continues to work transparently

### 2.2 Implementation Components

#### ITokenCacheSerializer (Custom)

```csharp
namespace TodoExtended.Web.Services;

public interface ITokenCacheSerializer
{
    Task<byte[]?> ReadCacheBytesAsync(string cacheKey);
    Task WriteCacheBytesAsync(string cacheKey, byte[] bytes);
    Task RemoveCacheAsync(string cacheKey);
}
```

#### SqliteTokenCacheSerializer

```csharp
namespace TodoExtended.Web.Services;

public class SqliteTokenCacheSerializer : ITokenCacheSerializer
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    
    public SqliteTokenCacheSerializer(
        AppDbContext db,
        IDataProtectionProvider dataProtection)
    {
        _db = db;
        _protector = dataProtection.CreateProtector("TokenCache");
    }
    
    public async Task<byte[]?> ReadCacheBytesAsync(string cacheKey)
    {
        var token = await _db.UserTokens.FindAsync(cacheKey);
        if (token is null) return null;
        
        var encryptedData = Convert.FromBase64String(token.EncryptedCacheData);
        return _protector.Unprotect(encryptedData);
    }
    
    public async Task WriteCacheBytesAsync(string cacheKey, byte[] bytes)
    {
        var encryptedData = _protector.Protect(bytes);
        var base64 = Convert.ToBase64String(encryptedData);
        
        var token = await _db.UserTokens.FindAsync(cacheKey);
        if (token is null)
        {
            token = new UserToken
            {
                UserId = cacheKey,
                EncryptedCacheData = base64,
                UpdatedUtc = DateTime.UtcNow,
            };
            _db.UserTokens.Add(token);
        }
        else
        {
            token.EncryptedCacheData = base64;
            token.UpdatedUtc = DateTime.UtcNow;
        }
        
        await _db.SaveChangesAsync();
    }
    
    public async Task RemoveCacheAsync(string cacheKey)
    {
        var token = await _db.UserTokens.FindAsync(cacheKey);
        if (token is not null)
        {
            _db.UserTokens.Remove(token);
            await _db.SaveChangesAsync();
        }
    }
}
```

#### MsalDistributedTokenCacheAdapter

Adapts MSAL's `ITokenCache` to our serializer. Based on Microsoft.Identity.Web's pattern but using our DB-backed serializer.

```csharp
using Microsoft.Identity.Client;

namespace TodoExtended.Web.Services;

public class MsalDistributedTokenCacheAdapter
{
    private readonly ITokenCacheSerializer _serializer;
    private readonly string _cacheKey;
    
    public MsalDistributedTokenCacheAdapter(
        ITokenCache tokenCache,
        ITokenCacheSerializer serializer,
        string cacheKey)
    {
        _serializer = serializer;
        _cacheKey = cacheKey;
        
        tokenCache.SetBeforeAccessAsync(OnBeforeAccessAsync);
        tokenCache.SetAfterAccessAsync(OnAfterAccessAsync);
    }
    
    private async Task OnBeforeAccessAsync(TokenCacheNotificationArgs args)
    {
        var data = await _serializer.ReadCacheBytesAsync(_cacheKey);
        if (data is not null)
        {
            args.TokenCache.DeserializeMsalV3(data);
        }
    }
    
    private async Task OnAfterAccessAsync(TokenCacheNotificationArgs args)
    {
        if (args.HasStateChanged)
        {
            var data = args.TokenCache.SerializeMsalV3();
            await _serializer.WriteCacheBytesAsync(_cacheKey, data);
        }
    }
}
```

### 2.3 User Registration & Token Capture

**Problem:** On first OIDC sign-in, we need to:
1. Extract user OID/email/name from claims
2. Create/update `User` record
3. Associate MSAL token cache with user

**Solution:** Custom middleware after authentication that runs on every request.

#### UserRegistrationMiddleware

```csharp
namespace TodoExtended.Web.Middleware;

public class UserRegistrationMiddleware(RequestDelegate next, ILogger<UserRegistrationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            var emailClaim = context.User.FindFirst("preferred_username") ?? context.User.FindFirst("email");
            var nameClaim = context.User.FindFirst("name");
            
            if (userIdClaim is not null && emailClaim is not null)
            {
                var userId = userIdClaim.Value;
                var user = await db.Users.FindAsync(userId);
                var now = DateTime.UtcNow;
                
                if (user is null)
                {
                    logger.LogInformation("Registering new user {UserId}", userId);
                    user = new User
                    {
                        Id = userId,
                        Email = emailClaim.Value,
                        DisplayName = nameClaim?.Value ?? emailClaim.Value,
                        CreatedUtc = now,
                        LastSeenUtc = now,
                    };
                    db.Users.Add(user);
                }
                else
                {
                    user.LastSeenUtc = now;
                }
                
                await db.SaveChangesAsync();
            }
        }
        
        await next(context);
    }
}
```

## 3. API Key Authentication

### 3.1 Custom Authentication Handler

Reads `X-Api-Key` header, validates against DB, and establishes user identity.

#### ApiKeyAuthenticationHandler

```csharp
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace TodoExtended.Web.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string Scheme => DefaultScheme;
    public string HeaderName { get; set; } = "X-Api-Key";
}

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }
        
        var providedKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrEmpty(providedKey))
        {
            return AuthenticateResult.NoResult();
        }
        
        var keyHash = ComputeHash(providedKey);
        
        var apiKey = await db.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && !k.IsRevoked);
        
        if (apiKey is null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }
        
        // Update last used timestamp (fire-and-forget to avoid blocking)
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = Context.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var key = await dbContext.ApiKeys.FindAsync(apiKey.Id);
                if (key is not null)
                {
                    key.LastUsedUtc = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to update API key last used timestamp");
            }
        });
        
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKey.UserId),
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", apiKey.UserId),
            new Claim(ClaimTypes.Name, apiKey.User!.DisplayName),
            new Claim(ClaimTypes.Email, apiKey.User.Email),
            new Claim("api_key_id", apiKey.Id.ToString()),
            new Claim("api_key_name", apiKey.Name),
        };
        
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        
        return AuthenticateResult.Success(ticket);
    }
    
    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
```

### 3.2 Graph Token Acquisition for API Key Users

**Challenge:** When a request is authenticated via API key (not interactive OIDC), `GraphServiceClient` still needs a valid MS Graph token.

**Solution:** Custom `ITokenAcquisition` wrapper that:
- For interactive users: delegate to Microsoft.Identity.Web's default implementation
- For API key users: manually construct MSAL `IConfidentialClientApplication` scoped to the user's cache

#### ApiKeyGraphTokenProvider

```csharp
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace TodoExtended.Web.Services;

public class ApiKeyGraphTokenProvider(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ITokenCacheSerializer tokenCacheSerializer,
    ITokenAcquisition defaultTokenAcquisition,
    ILogger<ApiKeyGraphTokenProvider> logger) : DelegatingAuthenticationProvider
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/.default";
    
    protected override async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var context = httpContextAccessor.HttpContext!;
        var user = context.User;
        
        // Check if this is an API key authenticated request
        var isApiKeyAuth = user.Identity?.AuthenticationType == ApiKeyAuthenticationOptions.DefaultScheme;
        
        if (!isApiKeyAuth)
        {
            // Delegate to default OIDC token acquisition
            return await defaultTokenAcquisition.GetAccessTokenForUserAsync(
                new[] { GraphBaseUrl },
                user: user);
        }
        
        // API key flow: manually acquire token using stored cache
        var userId = user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        if (userId is null)
        {
            throw new InvalidOperationException("User ID claim not found");
        }
        
        logger.LogDebug("Acquiring Graph token for API key user {UserId}", userId);
        
        var azureAdConfig = configuration.GetSection("AzureAd");
        var app = ConfidentialClientApplicationBuilder
            .Create(azureAdConfig["ClientId"])
            .WithClientSecret(azureAdConfig["ClientSecret"])
            .WithAuthority($"{azureAdConfig["Instance"]}{azureAdConfig["TenantId"]}")
            .Build();
        
        // Attach user's token cache
        _ = new MsalDistributedTokenCacheAdapter(app.UserTokenCache, tokenCacheSerializer, userId);
        
        var accounts = await app.GetAccountsAsync();
        var account = accounts.FirstOrDefault();
        
        if (account is null)
        {
            throw new InvalidOperationException(
                $"No cached account found for user {userId}. User must sign in via browser first.");
        }
        
        AuthenticationResult result;
        try
        {
            result = await app.AcquireTokenSilent(new[] { GraphBaseUrl }, account)
                .ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException)
        {
            throw new InvalidOperationException(
                "Token refresh failed. User must re-authenticate via browser.");
        }
        
        return result.AccessToken;
    }
}
```

## 4. API Endpoint Design

### 4.1 Minimal API Endpoints

Add to `Program.cs` after middleware setup:

```csharp
var apiGroup = app.MapGroup("/api")
    .RequireAuthorization();  // Accepts both OIDC and ApiKey schemes

// GET /api/templates
apiGroup.MapGet("/templates", async (ITemplateService templateService) =>
{
    var templates = await templateService.GetAllAsync();
    return Results.Ok(templates.Select(t => new
    {
        t.Id,
        t.Title,
        t.TaskListId,
        t.TaskListName,
        t.DueDateToday,
        t.SortOrder
    }));
});

// POST /api/templates/{id}/execute
apiGroup.MapPost("/templates/{id}/execute", async (
    int id,
    ITemplateService templateService) =>
{
    try
    {
        var task = await templateService.ExecuteTemplateAsync(id);
        return Results.Ok(new
        {
            task.Id,
            task.Title,
            task.Body,
            task.IsCompleted,
            task.DueDate,
            task.Importance
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (MicrosoftIdentityWebChallengeUserException)
    {
        return Results.Problem(
            "Graph API authorization required. Please sign in via browser.",
            statusCode: 401);
    }
});

// GET /api/today
apiGroup.MapGet("/today", async (ITodoService todoService) =>
{
    try
    {
        var tasks = await todoService.GetTodayTasksAsync();
        return Results.Ok(tasks.Select(t => new
        {
            t.Id,
            t.Title,
            t.Body,
            t.IsCompleted,
            t.DueDate,
            t.Importance,
            t.ListId,
            t.ListName
        }));
    }
    catch (MicrosoftIdentityWebChallengeUserException)
    {
        return Results.Problem(
            "Graph API authorization required. Please sign in via browser.",
            statusCode: 401);
    }
});
```

### 4.2 API Key Management Endpoints (for UI)

```csharp
// GET /api/keys - List user's API keys
apiGroup.MapGet("/keys", async (HttpContext context, AppDbContext db) =>
{
    var userId = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
    if (userId is null) return Results.Unauthorized();
    
    var keys = await db.ApiKeys
        .Where(k => k.UserId == userId && !k.IsRevoked)
        .OrderByDescending(k => k.CreatedUtc)
        .Select(k => new
        {
            k.Id,
            k.Name,
            k.CreatedUtc,
            k.LastUsedUtc
        })
        .ToListAsync();
    
    return Results.Ok(keys);
});

// POST /api/keys - Create new API key
apiGroup.MapPost("/keys", async (
    CreateApiKeyRequest request,
    HttpContext context,
    AppDbContext db) =>
{
    var userId = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
    if (userId is null) return Results.Unauthorized();
    
    // Generate secure random key (32 bytes = 256 bits)
    var keyBytes = RandomNumberGenerator.GetBytes(32);
    var plainKey = Convert.ToBase64String(keyBytes);
    var keyHash = ComputeHash(plainKey);
    
    var apiKey = new ApiKey
    {
        UserId = userId,
        Name = request.Name,
        KeyHash = keyHash,
        CreatedUtc = DateTime.UtcNow,
        IsRevoked = false,
    };
    
    db.ApiKeys.Add(apiKey);
    await db.SaveChangesAsync();
    
    // Return plain key ONLY on creation (never stored, never shown again)
    return Results.Ok(new
    {
        apiKey.Id,
        apiKey.Name,
        Key = plainKey,  // ⚠️ Show once, then forget
        apiKey.CreatedUtc
    });
});

// DELETE /api/keys/{id} - Revoke API key
apiGroup.MapDelete("/keys/{id}", async (
    int id,
    HttpContext context,
    AppDbContext db) =>
{
    var userId = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
    if (userId is null) return Results.Unauthorized();
    
    var key = await db.ApiKeys.FindAsync(id);
    if (key is null || key.UserId != userId)
    {
        return Results.NotFound();
    }
    
    key.IsRevoked = true;
    await db.SaveChangesAsync();
    
    return Results.NoContent();
});

record CreateApiKeyRequest(string Name);

static string ComputeHash(string input)
{
    var bytes = System.Text.Encoding.UTF8.GetBytes(input);
    var hash = System.Security.Cryptography.SHA256.HashData(bytes);
    return Convert.ToHexString(hash);
}
```

## 5. Service Changes

### 5.1 GraphServiceClient Registration

**Current (Program.cs):**
```csharp
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(["Tasks.ReadWrite", "User.Read"])
    .AddMicrosoftGraph(builder.Configuration.GetSection("Graph"))
    .AddInMemoryTokenCaches();
```

**New:**
```csharp
// 1. Add Data Protection (for token encryption)
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// 2. Register token cache serializer
builder.Services.AddScoped<ITokenCacheSerializer, SqliteTokenCacheSerializer>();

// 3. Add authentication with custom distributed token cache
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(["Tasks.ReadWrite", "User.Read"])
    .AddInMemoryTokenCaches();  // ⚠️ This stays, but we'll override for persistence

// Replace in-memory with distributed cache via custom token cache adapter
// (Hook into MSAL's token cache events after services are built)

// 4. Add API Key authentication
builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.DefaultScheme, null);

// 5. Configure authorization to accept both schemes
builder.Services.AddAuthorization(options =>
{
    var defaultPolicy = new AuthorizationPolicyBuilder(
        OpenIdConnectDefaults.AuthenticationScheme,
        ApiKeyAuthenticationOptions.DefaultScheme)
        .RequireAuthenticatedUser()
        .Build();
    
    options.DefaultPolicy = defaultPolicy;
});

// 6. Register custom Graph token provider
builder.Services.AddScoped<ApiKeyGraphTokenProvider>();

// 7. Replace GraphServiceClient with custom provider
builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var provider = sp.GetRequiredService<ApiKeyGraphTokenProvider>();
    return new GraphServiceClient(provider);
});
```

### 5.2 New Service: IApiKeyService

Optional abstraction for key generation/management (can also inline in endpoints).

```csharp
namespace TodoExtended.Web.Services;

public interface IApiKeyService
{
    Task<(ApiKey Key, string PlainKey)> CreateKeyAsync(string userId, string name);
    Task<IReadOnlyList<ApiKey>> GetUserKeysAsync(string userId);
    Task RevokeKeyAsync(int keyId, string userId);
    Task<ApiKey?> ValidateKeyAsync(string plainKey);
}
```

## 6. DI and Middleware Changes

### 6.1 Program.cs Full Integration

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using TodoExtended.Web.Authentication;
using TodoExtended.Web.Components;
using TodoExtended.Web.Data;
using TodoExtended.Web.Middleware;
using TodoExtended.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Data Protection (for token encryption)
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Token cache serializer
builder.Services.AddScoped<ITokenCacheSerializer, SqliteTokenCacheSerializer>();

// Authentication with Microsoft Entra ID
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(["Tasks.ReadWrite", "User.Read"])
    .AddInMemoryTokenCaches();  // Base setup; extended below

// API Key authentication
builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.DefaultScheme, null);

// Authorization: accept both OIDC and ApiKey
builder.Services.AddAuthorization(options =>
{
    var policy = new AuthorizationPolicyBuilder(
        OpenIdConnectDefaults.AuthenticationScheme,
        ApiKeyAuthenticationOptions.DefaultScheme)
        .RequireAuthenticatedUser()
        .Build();
    
    options.DefaultPolicy = policy;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

// Custom Graph token provider (handles API key users)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ApiKeyGraphTokenProvider>();
builder.Services.AddScoped<ITokenAcquisition>(sp =>
    sp.GetRequiredService<ITokenAcquisition>());  // Keep default available

builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var provider = sp.GetRequiredService<ApiKeyGraphTokenProvider>();
    return new GraphServiceClient(provider);
});

// App services
builder.Services.Configure<TodoCacheOptions>(builder.Configuration.GetSection("TodoCache"));
builder.Services.AddScoped<GraphTodoService>();
builder.Services.AddScoped<ITodoService, CachedTodoService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Auto-migrate database at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<UserRegistrationMiddleware>();  // ⚠️ After UseAuthentication
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

// API endpoints
var apiGroup = app.MapGroup("/api").RequireAuthorization();

apiGroup.MapGet("/templates", async (ITemplateService templateService) =>
{
    var templates = await templateService.GetAllAsync();
    return Results.Ok(templates);
});

apiGroup.MapPost("/templates/{id}/execute", async (
    int id,
    ITemplateService templateService) =>
{
    try
    {
        var task = await templateService.ExecuteTemplateAsync(id);
        return Results.Ok(task);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

apiGroup.MapGet("/today", async (ITodoService todoService) =>
{
    var tasks = await todoService.GetTodayTasksAsync();
    return Results.Ok(tasks);
});

// API Key management endpoints (keys.json abbreviated; see section 4.2)
apiGroup.MapGet("/keys", async (HttpContext context, AppDbContext db) => { /* ... */ });
apiGroup.MapPost("/keys", async (CreateApiKeyRequest request, HttpContext context, AppDbContext db) => { /* ... */ });
apiGroup.MapDelete("/keys/{id}", async (int id, HttpContext context, AppDbContext db) => { /* ... */ });

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

record CreateApiKeyRequest(string Name);
```

### 6.2 Distributed Token Cache Initialization

**Problem:** We need to wire up the distributed token cache to Microsoft.Identity.Web's MSAL pipeline.

**Solution:** Post-configuration hook that attaches our adapter to the token cache.

Add after building the app or via `IStartupFilter`:

```csharp
// In Program.cs after services are built (before app.Run()):
using (var scope = app.Services.CreateScope())
{
    var tokenAcquisition = scope.ServiceProvider.GetRequiredService<ITokenAcquisition>();
    var serializer = scope.ServiceProvider.GetRequiredService<ITokenCacheSerializer>();
    
    // This requires accessing MSAL's internal token cache
    // Microsoft.Identity.Web exposes this via ITokenCacheProvider or similar
    // Alternatively, hook via OnTokenValidated in OIDC events
}
```

**Alternative (cleaner):** Hook into OIDC events in `Program.cs`:

```csharp
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    var originalOnTokenValidated = options.Events.OnTokenValidated;
    
    options.Events.OnTokenValidated = async context =>
    {
        await originalOnTokenValidated(context);
        
        // At this point, MSAL has already cached tokens in-memory
        // We need to persist them to DB
        var userId = context.Principal?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        if (userId is not null)
        {
            var serializer = context.HttpContext.RequestServices.GetRequiredService<ITokenCacheSerializer>();
            var tokenAcquisition = context.HttpContext.RequestServices.GetRequiredService<ITokenAcquisition>();
            
            // Trigger a silent token acquisition to ensure cache is populated
            // This will trigger our cache serialization callbacks
            // (Implementation detail: may need to access MSAL's UserTokenCache directly)
        }
    };
});
```

**Note:** The exact wiring may require accessing Microsoft.Identity.Web internals or using their extensibility points. The pattern above is conceptually correct; implementation may need adjustment based on library version.

## 7. Security Considerations

### 7.1 API Key Security

- **Generation:** 256-bit random keys via `RandomNumberGenerator`
- **Storage:** Only SHA256 hash stored in DB (no plain text)
- **Transmission:** HTTPS enforced; key sent in `X-Api-Key` header
- **Revocation:** Soft delete via `IsRevoked` flag
- **Rotation:** Users can create multiple keys; old keys revoked when replaced

### 7.2 Token Security

- **Encryption at Rest:** Use ASP.NET Core Data Protection to encrypt MSAL cache blobs
- **Key Management:** Data Protection keys stored in `DataProtectionKeys` table (auto-created)
- **Rotation:** MSAL handles token refresh automatically
- **Scope:** Tokens scoped per user (1:1 relationship)

### 7.3 Data Protection Setup

Add migration for Data Protection keys:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ... existing ...
    
    modelBuilder.Entity<DataProtectionKey>(entity =>
    {
        entity.HasKey(e => e.Id);
    });
}
```

### 7.4 Rate Limiting (Future Enhancement)

Consider adding rate limiting middleware to API endpoints:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 60;
    });
});

// Apply to API group
apiGroup.RequireRateLimiting("api");
```

## 8. Migration Path

### 8.1 Database Migration

1. Add new entities to `AppDbContext`
2. Run `dotnet ef migrations add AddApiKeyAndTokenStorage`
3. Auto-migrate will apply on next startup

### 8.2 Deployment Steps

1. Deploy code with new authentication handlers
2. Existing users: on next OIDC sign-in, `UserRegistrationMiddleware` creates `User` record and starts persisting tokens
3. Users can then create API keys via UI
4. No breaking changes to existing Blazor pages (they continue using OIDC)

## 9. Testing Strategy

### 9.1 Manual Testing

1. **OIDC Flow:** Sign in via browser → verify `User` record created → check `UserToken` table populated
2. **API Key Creation:** Create key via UI → verify hash stored, plain key shown once
3. **API Key Auth:** Call `GET /api/templates` with `X-Api-Key` header → verify 200 response
4. **Graph Token Acquisition:** Call `POST /api/templates/{id}/execute` → verify task created in MS To Do
5. **Token Refresh:** Wait for token expiry → call API → verify MSAL silently refreshes from stored refresh token

### 9.2 Automated Tests (Future)

- Unit test `ApiKeyAuthenticationHandler` with mock DB
- Integration test API endpoints with test user + API key
- Test token cache serialization/deserialization
- Test API key revocation

## 10. UI Integration

### 10.1 New Page: API Keys Management

Create `Components/Pages/ApiKeys.razor`:

```razor
@page "/api-keys"
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize]

<h3>API Keys</h3>

<!-- List existing keys -->
<!-- Button to create new key -->
<!-- Modal to show newly created key (with warning to copy now) -->
<!-- Revoke button per key -->
```

Add to NavMenu:

```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="api-keys">
        <span class="bi bi-key-fill" aria-hidden="true"></span> API Keys
    </NavLink>
</div>
```

## 11. Documentation for Users

### 11.1 API Key Usage Example

```bash
# Get your templates
curl -H "X-Api-Key: YOUR_API_KEY_HERE" https://localhost:5001/api/templates

# Execute a template
curl -X POST -H "X-Api-Key: YOUR_API_KEY_HERE" \
  https://localhost:5001/api/templates/1/execute

# Get today's tasks
curl -H "X-Api-Key: YOUR_API_KEY_HERE" https://localhost:5001/api/today
```

### 11.2 Security Warning

**When creating a key:**
> ⚠️ **Copy this key now!** It will only be shown once. Store it securely. If lost, you must create a new key.

## 12. Open Questions & Future Work

1. **Key Expiration:** Should API keys have TTL? Add `ExpiresUtc` field?
2. **Scopes per Key:** Should keys have different permission levels (read-only, execute-only)?
3. **Audit Logging:** Track API key usage beyond `LastUsedUtc`?
4. **Multi-Factor for Key Creation:** Require re-auth before generating keys?
5. **MSAL Token Cache Cleanup:** Periodic cleanup of expired `UserToken` records?
6. **CORS:** If API used from browser clients, configure CORS policy

## Summary

This design enables API key authentication by:
1. **Storing hashed API keys** per user in SQLite
2. **Persisting MSAL token caches** (encrypted) in the DB
3. **Custom authentication handler** for API key validation
4. **Custom Graph token provider** that works for both OIDC and API key flows
5. **Three REST endpoints** for templates and today's tasks
6. **Minimal changes** to existing services (additive, not disruptive)

The approach leverages Microsoft.Identity.Web's MSAL infrastructure rather than reinventing token management, ensuring compatibility with token refresh, consent flows, and Graph API best practices.
