using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace TodoExtended.E2E;

/// <summary>
/// Captures viewport screenshots of every app view across two themes (light/dark)
/// and two viewports (desktop 1280×800, mobile 390×844).
/// Output lands in docs/screenshots/ at the repo root.
/// Requires the app running with Demo__Enabled=true.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class ScreenshotCaptureTest : PageTest
{
    private string BaseUrl => Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5000";

    // Repo root: test project is at tests/TodoExtended.E2E/ → go up 3 from bin/Debug/net10.0/
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static string ScreenshotsDir => Path.Combine(RepoRoot, "docs", "screenshots");

    private record ViewportSpec(string Name, int Width, int Height);
    private record PageSpec(string ViewName, string RelativeUrl, string WaitSelector);

    private static readonly ViewportSpec[] Viewports =
    [
        new("desktop", 1280, 800),
        new("mobile", 390, 844),
    ];

    private static readonly PageSpec[] Pages =
    [
        new("home", "/", "h2:has-text('Quick Create'), h3:has-text('No templates yet')"),
        new("today", "/today", "h1:has-text('Today')"),
        new("tasks", "/tasks", "h1:has-text('Tasks'), h2:has-text('Pick a list')"),
        new("templates", "/templates", "h1:has-text('Templates')"),
        new("sync-settings", "/sync-settings", "h1:has-text('Sync Settings')"),
        new("api-keys", "/api-keys", "h1:has-text('API Keys')"),
    ];

    [Test]
    public async Task CaptureAllViewScreenshots()
    {
        Directory.CreateDirectory(ScreenshotsDir);

        // Sign in via demo mode
        await SignInViaDemoAsync();

        foreach (var page in Pages)
        {
            foreach (var theme in new[] { "dark", "light" })
            {
                foreach (var vp in Viewports)
                {
                    try
                    {
                        await CaptureScreenshotAsync(page, theme, vp);
                    }
                    catch (Exception ex)
                    {
                        TestContext.WriteLine($"[WARN] Failed: {page.ViewName}--{vp.Name}-{theme}: {ex.Message}");
                    }
                }
            }
        }

        // Attempt templates-dialog screenshots
        await CaptureTemplatesDialogScreenshots();

        // Capture sidebar-open on mobile
        await CaptureSidebarOpenScreenshots();
    }

    private async Task SignInViaDemoAsync()
    {
        await Page.GotoAsync(BaseUrl);

        var demoButton = Page.GetByRole(AriaRole.Link, new() { Name = "Try Demo" });
        await Expect(demoButton).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await demoButton.ClickAsync();

        // Wait for authenticated state — "User menu" button is always visible in the sidebar
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    private async Task CaptureScreenshotAsync(PageSpec spec, string theme, ViewportSpec vp)
    {
        // Set viewport
        await Page.SetViewportSizeAsync(vp.Width, vp.Height);

        // Navigate
        await Page.GotoAsync($"{BaseUrl}{spec.RelativeUrl}", new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for content to appear
        await Page.Locator(spec.WaitSelector).First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

        // Set theme
        await SetThemeAsync(theme);

        // Brief settle time for layout/animations
        await Page.WaitForTimeoutAsync(500);

        var fileName = $"{spec.ViewName}--{vp.Name}-{theme}.png";
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Combine(ScreenshotsDir, fileName),
        });

        TestContext.WriteLine($"[OK] {fileName}");
    }

    private async Task SetThemeAsync(string theme)
    {
        bool wantDark = theme == "dark";

        // Directly toggle the Tailwind dark mode class via JavaScript for reliability.
        // The Blazor Server circuit may not be connected yet, making @onclick unreliable.
        // For screenshot purposes, the visual appearance is what matters.
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

    private async Task CaptureTemplatesDialogScreenshots()
    {
        foreach (var theme in new[] { "dark", "light" })
        {
            foreach (var vp in Viewports)
            {
                try
                {
                    // Navigate fresh each time to avoid stale Blazor state
                    await Page.SetViewportSizeAsync(vp.Width, vp.Height);
                    await Page.GotoAsync($"{BaseUrl}/templates", new() { WaitUntil = WaitUntilState.NetworkIdle });
                    await Page.Locator("h1:has-text('Templates')").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

                    // Set theme before opening dialog
                    await SetThemeAsync(theme);
                    await Page.WaitForTimeoutAsync(500);

                    // Wait for Blazor circuit so button clicks work
                    await Page.WaitForTimeoutAsync(3_000);

                    // Open dialog
                    var newButton = Page.Locator("button:has-text('New Template')");
                    if (await newButton.CountAsync() == 0)
                    {
                        TestContext.WriteLine("[WARN] No 'New Template' button found — skipping templates-dialog screenshots.");
                        return;
                    }

                    await newButton.First.ClickAsync();

                    // Wait for dialog modal
                    var dialogLocator = Page.Locator("div.fixed.inset-0.z-50");
                    await dialogLocator.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
                    await Page.WaitForTimeoutAsync(500);

                    var fileName = $"templates-dialog--{vp.Name}-{theme}.png";
                    await Page.ScreenshotAsync(new()
                    {
                        Path = Path.Combine(ScreenshotsDir, fileName),
                    });
                    TestContext.WriteLine($"[OK] {fileName}");
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"[WARN] Failed: templates-dialog--{vp.Name}-{theme}: {ex.Message}");
                }
            }
        }
    }

    private async Task CaptureSidebarOpenScreenshots()
    {
        var mobileVp = new ViewportSpec("mobile", 390, 844);

        foreach (var theme in new[] { "dark", "light" })
        {
            try
            {
                await Page.SetViewportSizeAsync(mobileVp.Width, mobileVp.Height);
                await Page.GotoAsync($"{BaseUrl}/today", new() { WaitUntil = WaitUntilState.NetworkIdle });
                await Page.Locator("h1:has-text('Today')").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

                // Set theme before opening sidebar
                await SetThemeAsync(theme);
                await Page.WaitForTimeoutAsync(500);

                // Wait for Blazor circuit so button click works
                await Page.WaitForTimeoutAsync(3_000);

                // Click the BottomBar toggle to open the sidebar (selector scoped to nav to avoid the hidden header button)
                var toggleButton = Page.Locator("nav button[aria-label='Toggle menu']").First;
                await toggleButton.ClickAsync();

                // Wait for sidebar to slide in
                await Page.WaitForTimeoutAsync(500);

                var fileName = $"sidebar-open--{mobileVp.Name}-{theme}.png";
                await Page.ScreenshotAsync(new()
                {
                    Path = Path.Combine(ScreenshotsDir, fileName),
                });
                TestContext.WriteLine($"[OK] {fileName}");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"[WARN] Failed: sidebar-open--{mobileVp.Name}-{theme}: {ex.Message}");
            }
        }
    }
}
