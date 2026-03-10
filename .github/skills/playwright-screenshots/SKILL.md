---
name: playwright-screenshots
description: "Capture Playwright screenshots with isolated database. Use when refreshing screenshots, updating PWA manifest images, or running E2E visual tests."
---

## Context

Screenshot tests in TodoExtended must demonstrate the app across multiple combinations of viewports and themes without polluting user data. The test captures 28 screenshots (7 views × 2 themes × 2 viewports) using an isolated SQLite database and direct DOM manipulation for theme toggling, avoiding Blazor Server circuit reliability issues with `@onclick` handlers during early page load.

## Patterns

### 1. Isolated Database Setup

Use a dedicated SQLite database file for screenshot runs to prevent demo/test data from mixing with real user data (cached by `CachedTodoService`).

**Environment variables:**
```
ConnectionStrings__DefaultConnection=Data Source=../../artifacts/todoextended-screenshots.db
ASPNETCORE_ENVIRONMENT=Development
```

**Launch pattern:**
```bash
dotnet run --no-launch-profile --urls http://localhost:5000 \
  --property ConnectionStrings:DefaultConnection=Data Source=../../artifacts/todoextended-screenshots.db
```

**Key point:** `ASPNETCORE_ENVIRONMENT=Development` is required—`MapStaticAssets()` serves 0-byte CSS in non-Development environments, producing unstyled screenshots. Avoid custom environment names.

**Cleanup:** Always delete `artifacts/todoextended-screenshots.db*` files (`.db`, `.db-wal`, `.db-shm`) before and after test runs to ensure clean state.

### 2. App Startup and Readiness

Start the app with demo mode enabled, listening on port 5000:

```bash
# Environment flags required
Demo__Enabled=true
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Data Source=../../artifacts/todoextended-screenshots.db
```

**Wait pattern:** Poll the base URL (`http://localhost:5000`) until it responds with HTTP 200 before running tests. Example timeout: 15 seconds.

### 3. Theme Toggling via DOM Manipulation

Blazor Server's `@onclick` handlers are unreliable in E2E tests shortly after page navigation (WebSocket circuit takes 2–3 seconds to connect). Instead, directly manipulate the DOM class list:

```csharp
private async Task SetThemeAsync(string theme)
{
    bool wantDark = theme == "dark";
    
    // Toggle the Tailwind dark mode class via JavaScript for reliability
    await Page.EvaluateAsync(@$"() => {{
        const root = document.body.querySelector('div');
        if (root) {{
            if ({(wantDark ? "true" : "false")}) {{
                root.classList.add('dark');
            }} else {{
                root.classList.remove('dark');
            }}
        }}
    }}");
    await Page.WaitForTimeoutAsync(300);
}
```

**Why:** The visual appearance is what matters for screenshots. Clicking the Blazor toggle button requires a fully connected circuit, introducing flakiness. DOM manipulation is instantaneous and reliable.

### 4. Screenshot Naming Convention

Use the pattern: `{view}--{device}-{theme}.png`

**Examples:**
- `today--desktop-dark.png` (Today view, desktop viewport, dark theme)
- `home--mobile-light.png` (Home view, mobile viewport, light theme)
- `templates-dialog--desktop-dark.png` (Templates dialog, desktop, dark)

**Viewport specifications:**
- Desktop: 1280 × 800
- Mobile: 390 × 844

### 5. Output Locations

**Primary output:** `docs/screenshots/` (at repo root)
- Contains all 28 screenshots from standard views + dialog variants
- Source of truth for documentation

**Secondary output:** `src/TodoExtended.Web/wwwroot/screenshots/`
- Contains selected dark-theme screenshots (4 files)
- Used by PWA manifest for app install UI
- Manually curated subset; not automatically generated

### 6. Manifest Synchronization

After updating screenshots in `wwwroot/screenshots/`, sync the `wwwroot/manifest.json` file's `screenshots` array:

```json
{
  "screenshots": [
    {
      "src": "screenshots/today--desktop-dark.png",
      "sizes": "1280x800",
      "form_factor": "wide"
    },
    {
      "src": "today--mobile-dark.png",
      "sizes": "390x844",
      "form_factor": "narrow"
    }
  ]
}
```

**Important:** Screenshot dimensions in the manifest must match exact viewport dimensions (1280×800 for desktop, 390×844 for mobile).

### 7. Test Execution

Run the screenshot test via:

```bash
dotnet test tests/TodoExtended.E2E/ --filter ScreenshotCaptureTest
```

**Typical flow:**
1. Test creates `docs/screenshots/` if it doesn't exist
2. Signs in via "Try Demo" link
3. Iterates all 7 views, each with 2 themes × 2 viewports = 28 screenshots
4. Attempts dialog screenshot capture
5. Logs success/warning per file

**Failure handling:** If a single screenshot fails (e.g., timeout waiting for content), the test logs a warning and continues—partial output is still useful.

## Examples

### Full Test Structure (from ScreenshotCaptureTest.cs)

```csharp
[Test]
public async Task CaptureAllViewScreenshots()
{
    Directory.CreateDirectory(ScreenshotsDir);
    
    await SignInViaDemoAsync();
    
    foreach (var page in Pages)
    {
        foreach (var theme in new[] { "dark", "light" })
        {
            foreach (var vp in Viewports)
            {
                await CaptureScreenshotAsync(page, theme, vp);
            }
        }
    }
}

private async Task CaptureScreenshotAsync(PageSpec spec, string theme, ViewportSpec vp)
{
    await Page.SetViewportSizeAsync(vp.Width, vp.Height);
    await Page.GotoAsync($"{BaseUrl}{spec.RelativeUrl}", 
        new() { WaitUntil = WaitUntilState.NetworkIdle });
    
    await Page.Locator(spec.WaitSelector).First
        .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
    
    await SetThemeAsync(theme);
    await Page.WaitForTimeoutAsync(500);
    
    var fileName = $"{spec.ViewName}--{vp.Name}-{theme}.png";
    await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotsDir, fileName) });
}
```

### Page Specifications

Define views with relative URLs and CSS selectors for content wait:

```csharp
private static readonly PageSpec[] Pages =
[
    new("home", "/", "h2:has-text('Quick Create'), h3:has-text('No templates yet')"),
    new("today", "/today", "h1:has-text('Today')"),
    new("tasks", "/tasks", "h1:has-text('Tasks'), h2:has-text('Pick a list')"),
    new("templates", "/templates", "h1:has-text('Templates')"),
    new("sync-settings", "/sync-settings", "h1:has-text('Sync Settings')"),
    new("api-keys", "/api-keys", "h1:has-text('API Keys')"),
];
```

### Demo Sign-In

```csharp
private async Task SignInViaDemoAsync()
{
    await Page.GotoAsync(BaseUrl);
    
    var demoButton = Page.GetByRole(AriaRole.Link, new() { Name = "Try Demo" });
    await Expect(demoButton).ToBeVisibleAsync(new() { Timeout = 15_000 });
    await demoButton.ClickAsync();
    
    // Wait for authenticated state
    await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Sign out" }))
        .ToBeVisibleAsync(new() { Timeout = 15_000 });
}
```

## Anti-Patterns

### ❌ Don't Click the Blazor Theme Toggle Button

```csharp
// UNRELIABLE: Blazor circuit may not be ready yet
await Page.Locator("button[aria-label='Toggle dark mode']").ClickAsync();
```

**Problem:** The WebSocket circuit that powers Blazor event handlers takes 2–3 seconds to connect after page load. Clicking the button before the circuit is ready results in a no-op or flakiness.

**Solution:** Use `SetThemeAsync()` with direct DOM manipulation instead.

### ❌ Don't Use Custom ASPNETCORE_ENVIRONMENT

```bash
# BAD: MapStaticAssets() serves 0-byte CSS in non-Development
ASPNETCORE_ENVIRONMENT=Demo dotnet run ...
```

**Problem:** `MapStaticAssets()` in Program.cs only works in the `Development` environment. Other environments skip asset serving, resulting in 0-byte CSS files and unstyled screenshots.

**Solution:** Always use `ASPNETCORE_ENVIRONMENT=Development`.

### ❌ Don't Use Real User Database

```csharp
// BAD: Demo data gets cached and pollutes user's local database
ConnectionStrings__DefaultConnection=Data Source=todoextended.db
```

**Problem:** `CachedTodoService` caches demo data permanently, corrupting the user's normal experience.

**Solution:** Use an isolated database with a unique path: `Data Source=../../artifacts/todoextended-screenshots.db`

### ❌ Don't Forget Cleanup

```csharp
// BAD: Stale database persists, causes false positives
await Page.ScreenshotAsync(new() { Path = filePath });
// ... test ends, .db file still there
```

**Problem:** Re-running the test reuses the old database state instead of starting fresh.

**Solution:** Delete `artifacts/todoextended-screenshots.db*` before and after test runs.

### ❌ Don't Skip Layout Settle Time

```csharp
// RISKY: Screenshot before animations complete
await SetThemeAsync(theme);
await Page.ScreenshotAsync(...);
```

**Problem:** Tailwind transitions and CSS animations may still be in progress, resulting in inconsistent screenshots.

**Solution:** Always add a brief wait after theme changes: `await Page.WaitForTimeoutAsync(500);`
