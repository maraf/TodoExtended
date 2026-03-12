using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using TodoExtended.Web.Api;
using TodoExtended.Web.Authentication;
using TodoExtended.Web.Components;
using TodoExtended.Web.Data;
using TodoExtended.Web.Middleware;
using TodoExtended.Web.Services;
using TodoExtended.Web.Services.AiChat;


var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection("Demo"));
var isDemoMode = builder.Configuration.GetValue<bool>("Demo:Enabled");

// Register IDistributedCache backed by SQLite (must be registered before authentication)
builder.Services.AddSingleton<Microsoft.Extensions.Caching.Distributed.IDistributedCache, SqliteDistributedCache>();

if (isDemoMode)
{
    // Demo mode: cookie-only authentication, no MS Graph required
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/";
        });

    builder.Services.AddSingleton<DemoDataStore>();
    builder.Services.AddScoped<IGraphTodoClient, DemoGraphTodoClient>();
}
else
{
    // Authentication with Microsoft Entra ID
    var graphScopes = builder.Configuration.GetSection("Graph:Scopes").Get<string[]>()!;
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
        .EnableTokenAcquisitionToCallDownstreamApi(graphScopes)
        .AddMicrosoftGraph(builder.Configuration.GetSection("Graph"))
        .AddDistributedTokenCaches();

    // Add API Key authentication
    builder.Services.AddAuthentication()
        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme,
            options => { });

    builder.Services.AddSingleton<ApiKeyGraphClientFactory>();

    // Override GraphServiceClient to handle both OIDC and API key authentication
    builder.Services.AddScoped<Microsoft.Graph.GraphServiceClient>(sp =>
    {
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        var context = httpContextAccessor.HttpContext;
        var isApiKey = context?.User.HasClaim("apikey", "true") == true;

        if (isApiKey && context != null)
        {
            // API key flow: use factory to create client with MSAL cache lookup
            var userId = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")!.Value;
            var factory = sp.GetRequiredService<ApiKeyGraphClientFactory>();
            return factory.CreateForUser(userId);
        }

        // OIDC flow: delegate to the default GraphServiceClient registered by AddMicrosoftGraph
        // We need to manually create it since we're overriding the registration
        var tokenAcquisition = sp.GetRequiredService<Microsoft.Identity.Web.ITokenAcquisition>();

        // Use the TokenAcquisition-based auth provider
        var authProvider = new Microsoft.Kiota.Abstractions.Authentication.BaseBearerTokenAuthenticationProvider(
            new OidcTokenProvider(tokenAcquisition, graphScopes));

        return new Microsoft.Graph.GraphServiceClient(authProvider);
    });

    builder.Services.AddScoped<IGraphTodoClient, HttpGraphTodoClient>();
}

// Both modes use GraphTodoService + CachedTodoService through IGraphTodoClient
builder.Services.AddScoped<GraphTodoService>();
builder.Services.AddScoped<ITodoService, CachedTodoService>();

builder.Services.AddAuthorization(options =>
{
    var policyBuilder = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser();

    if (isDemoMode)
    {
        policyBuilder.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
    }
    else
    {
        policyBuilder.AddAuthenticationSchemes(
            OpenIdConnectDefaults.AuthenticationScheme,
            ApiKeyAuthenticationOptions.DefaultScheme);
    }

    options.DefaultPolicy = policyBuilder.Build();
});
builder.Services.AddCascadingAuthenticationState();
var controllers = builder.Services.AddControllersWithViews();
if (!isDemoMode)
    controllers.AddMicrosoftIdentityUI();

// EF Core + SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var dbPath = connectionString.Replace("Data Source=", "");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);

builder.Services.AddSingleton<EnableForeignKeysInterceptor>();

// Scoped DbContext for regular use
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlite(connectionString)
           .AddInterceptors(sp.GetRequiredService<EnableForeignKeysInterceptor>()));

// Singleton DbContext factory for singleton services (like SqliteDistributedCache)
builder.Services.AddSingleton<IDbContextFactory<AppDbContext>>(provider => 
    new SimpleDbContextFactory(connectionString, provider.GetRequiredService<EnableForeignKeysInterceptor>()));

builder.Services.Configure<TodoCacheOptions>(builder.Configuration.GetSection("TodoCache"));
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IUserTimeZoneService, UserTimeZoneService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<INotificationService, NotificationService>();

// AI Chat
builder.Services.Configure<AiChatOptions>(builder.Configuration.GetSection(AiChatOptions.SectionName));
var aiChatApiKey = builder.Configuration.GetValue<string>("AiChat:ApiKey");

if (isDemoMode)
{
    builder.Services.AddScoped<IChatService, DemoChatService>();
}
else if (!string.IsNullOrEmpty(aiChatApiKey))
{
    var aiChatEndpoint = builder.Configuration.GetValue<string>("AiChat:Endpoint") ?? "https://models.github.ai/inference";
    var aiChatModel = builder.Configuration.GetValue<string>("AiChat:Model") ?? "openai/gpt-4.1-mini";

    builder.Services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(_ =>
    {
        var openAiClient = new OpenAI.Chat.ChatClient(
            aiChatModel,
            new System.ClientModel.ApiKeyCredential(aiChatApiKey),
            new OpenAI.OpenAIClientOptions { Endpoint = new Uri(aiChatEndpoint) });
        return openAiClient.AsIChatClient();
    });
    builder.Services.AddScoped<IChatService, ChatService>();
}
else
{
    builder.Services.AddScoped<IChatService, StubChatService>();
}

builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 512 * 1024); // 512 KB for large PersistentState payloads
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Auto-migrate database at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Enable WAL mode for concurrent read/write during parallel sync
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

    // Seed demo user so the API keys page (and its FK constraint) works in demo mode
    if (isDemoMode)
    {
        if (!db.Users.Any(u => u.Id == "demo-user"))
        {
            db.Users.Add(new User
            {
                Id = "demo-user",
                Email = "demo@example.com",
                DisplayName = "Demo User",
                CreatedUtc = DateTime.UtcNow,
                LastSeenUtc = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        // Seed demo API keys so the API Keys screen shows something
        if (!db.ApiKeys.Any(k => k.UserId == "demo-user"))
        {
            var now = DateTime.UtcNow;
            static string HashKey(string key) =>
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

            db.ApiKeys.AddRange(
                new ApiKey
                {
                    UserId = "demo-user",
                    Name = "Garmin Watch",
                    KeyHash = HashKey("tek_demo_garmin_placeholder"),
                    CreatedUtc = now.AddMonths(-3),
                    LastUsedUtc = now.AddDays(-7),
                    IsRevoked = false
                },
                new ApiKey
                {
                    UserId = "demo-user",
                    Name = "Mobile App",
                    KeyHash = HashKey("tek_demo_mobile_placeholder"),
                    CreatedUtc = now.AddMonths(-1),
                    LastUsedUtc = now.AddDays(-1),
                    IsRevoked = false
                },
                new ApiKey
                {
                    UserId = "demo-user",
                    Name = "Home Automation",
                    KeyHash = HashKey("tek_demo_home_placeholder"),
                    CreatedUtc = now.AddDays(-14),
                    LastUsedUtc = null,
                    IsRevoked = false
                }
            );
            db.SaveChanges();
        }

        // Seed demo templates so the Templates screen shows something
        if (!db.TaskTemplates.Any(t => t.UserId == "demo-user"))
        {
            const string WorkListId = "demo-list-work";
            const string WorkListName = "📋 Work";
            const string PersonalListId = "demo-list-personal";
            const string PersonalListName = "🏠 Personal";
            const string LearningListId = "demo-list-learning";
            const string LearningListName = "📚 Learning";

            db.TaskTemplates.AddRange(
                new TaskTemplate
                {
                    Title = "Daily standup notes",
                    TaskListId = WorkListId,
                    TaskListName = WorkListName,
                    DueDateToday = true,
                    ReminderTime = new TimeOnly(9, 0),
                    SortOrder = 1,
                    UserId = "demo-user"
                },
                new TaskTemplate
                {
                    Title = "Weekly review",
                    TaskListId = WorkListId,
                    TaskListName = WorkListName,
                    DueDateToday = true,
                    ReminderTime = null,
                    SortOrder = 2,
                    UserId = "demo-user"
                },
                new TaskTemplate
                {
                    Title = "Buy groceries",
                    TaskListId = PersonalListId,
                    TaskListName = PersonalListName,
                    DueDateToday = false,
                    ReminderTime = null,
                    SortOrder = 0,
                    UserId = "demo-user"
                },
                new TaskTemplate
                {
                    Title = "Morning exercise",
                    TaskListId = PersonalListId,
                    TaskListName = PersonalListName,
                    DueDateToday = true,
                    ReminderTime = new TimeOnly(7, 0),
                    SortOrder = 1,
                    UserId = "demo-user"
                },
                new TaskTemplate
                {
                    Title = "Read 30 minutes",
                    TaskListId = LearningListId,
                    TaskListName = LearningListName,
                    DueDateToday = true,
                    ReminderTime = new TimeOnly(21, 0),
                    SortOrder = 1,
                    UserId = "demo-user"
                }
            );
            db.SaveChanges();
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<UserSyncMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Demo auth endpoints (only active in demo mode)
if (isDemoMode)
{
    app.MapGet("/auth/demo-signin", async (HttpContext context) =>
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "demo-user"),
            new System.Security.Claims.Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "demo-user"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Demo User"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "demo@example.com"),
            new System.Security.Claims.Claim("demo", "true"),
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        context.Response.Redirect("/");
    }).AllowAnonymous();

    app.MapGet("/auth/demo-signout", async (HttpContext context) =>
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/");
    });
}

// API Endpoints
var api = app.MapGroup("/api").RequireAuthorization().DisableAntiforgery();

static string GetUserId(HttpContext context) =>
    context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
    ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
    ?? throw new UnauthorizedAccessException("User ID not found in claims");

// Template endpoints
api.MapGet("/templates", async (HttpContext context, ITemplateService templateService) =>
{
    var userId = GetUserId(context);
    var templates = await templateService.GetAllAsync(userId);
    return Results.Ok(templates);
});

api.MapPost("/templates/{id}/execute", async (Guid id, HttpContext context, ITemplateService templateService) =>
{
    var userId = GetUserId(context);
    try
    {
        var task = await templateService.ExecuteTemplateAsync(id, userId);
        return Results.Ok(new ApiTodoTask(task.Id, task.Title, task.IsCompleted, task.DueDate, task.Importance));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

// Today's tasks endpoint
api.MapGet("/today", async (HttpContext context, ITodoService todoService) =>
{
    var userId = GetUserId(context);
    var tasks = await todoService.GetTodayTasksAsync(userId);
    return Results.Ok(tasks.Select(t => new ApiTodoTaskWithList(
        t.Id, t.Title, t.IsCompleted, t.DueDate, t.Importance, t.ListId, t.ListName)));
});

// Mark task as completed
api.MapPost("/tasks/{taskListId}/{taskId}/complete", async (string taskListId, string taskId, HttpContext context, ITodoService todoService) =>
{
    var userId = GetUserId(context);
    await todoService.UpdateTaskStatusAsync(taskListId, taskId, completed: true, userId);
    return Results.Ok(new { status = "completed" });
});

// Synced task lists
api.MapGet("/tasklists", async (HttpContext context, ITodoService todoService) =>
{
    var userId = GetUserId(context);
    var lists = await todoService.GetTaskListsAsync(userId);
    return Results.Ok(lists.Where(l => l.IsSynced).Select(l => new ApiTaskList(l.Id, l.DisplayName)));
});

// Tasks for a specific list
api.MapGet("/tasklists/{listId}/tasks", async (string listId, HttpContext context, ITodoService todoService) =>
{
    var userId = GetUserId(context);
    var tasks = await todoService.GetTasksAsync(listId, userId);
    return Results.Ok(tasks.Select(t => new ApiTodoTask(t.Id, t.Title, t.IsCompleted, t.DueDate, t.Importance)));
});

app.Run();
