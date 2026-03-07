using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using TodoExtended.Web.Api;
using TodoExtended.Web.Authentication;
using TodoExtended.Web.Components;
using TodoExtended.Web.Data;
using TodoExtended.Web.Middleware;
using MudBlazor.Services;
using TodoExtended.Web.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// Register IDistributedCache backed by SQLite (must be registered before authentication)
builder.Services.AddSingleton<Microsoft.Extensions.Caching.Distributed.IDistributedCache, SqliteDistributedCache>();

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

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(
            OpenIdConnectDefaults.AuthenticationScheme,
            ApiKeyAuthenticationOptions.DefaultScheme)
        .Build();
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

// EF Core + SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var dbPath = connectionString.Replace("Data Source=", "");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);

// Scoped DbContext for regular use
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Singleton DbContext factory for singleton services (like SqliteDistributedCache)
builder.Services.AddSingleton<IDbContextFactory<AppDbContext>>(provider => 
    new SimpleDbContextFactory(connectionString));

builder.Services.Configure<TodoCacheOptions>(builder.Configuration.GetSection("TodoCache"));
builder.Services.AddScoped<GraphTodoService>();
builder.Services.AddScoped<ITodoService, CachedTodoService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IUserTimeZoneService, UserTimeZoneService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddSingleton<ApiKeyGraphClientFactory>();
builder.Services.AddHttpContextAccessor();

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

builder.Services.AddMudServices();

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

// API Endpoints
var api = app.MapGroup("/api").RequireAuthorization().DisableAntiforgery();

// Template endpoints
api.MapGet("/templates", async (ITemplateService templateService) =>
{
    var templates = await templateService.GetAllAsync();
    return Results.Ok(templates);
});

api.MapPost("/templates/{id}/execute", async (Guid id, ITemplateService templateService) =>
{
    try
    {
        var task = await templateService.ExecuteTemplateAsync(id);
        return Results.Ok(new ApiTodoTask(task.Id, task.Title, task.IsCompleted, task.DueDate, task.Importance));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

// Today's tasks endpoint
api.MapGet("/today", async (ITodoService todoService) =>
{
    var tasks = await todoService.GetTodayTasksAsync();
    return Results.Ok(tasks.Select(t => new ApiTodoTaskWithList(
        t.Id, t.Title, t.IsCompleted, t.DueDate, t.Importance, t.ListId, t.ListName)));
});

// Mark task as completed
api.MapPost("/tasks/{taskListId}/{taskId}/complete", async (string taskListId, string taskId, ITodoService todoService) =>
{
    await todoService.UpdateTaskStatusAsync(taskListId, taskId, completed: true);
    return Results.Ok(new { status = "completed" });
});

app.Run();
