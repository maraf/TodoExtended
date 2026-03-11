using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace TodoExtended.E2E;

/// <summary>
/// Playwright E2E test that signs in using the demo account and takes a screenshot.
/// Requires the app to be running with Demo:Enabled=true.
/// Set the E2E_BASE_URL environment variable to the app base URL (default: http://localhost:5000).
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class DemoSignInTest : PageTest
{
    private string BaseUrl => Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5000";

    [Test]
    public async Task SignInAsDemo_ShowsAuthenticatedHomePage_AndTakesScreenshot()
    {
        // Navigate to the home page (unauthenticated)
        await Page.GotoAsync(BaseUrl);

        // Wait for the "Try Demo" button to be visible
        var demoButton = Page.GetByRole(AriaRole.Link, new() { Name = "Try Demo" });
        await Expect(demoButton).ToBeVisibleAsync();

        // Click "Try Demo" — data-enhance-nav="false" on the button forces a real browser
        // navigation (not Blazor's fetch-based enhanced nav), ensuring the Set-Cookie header
        // from /auth/demo-signin is stored before / is loaded.
        await demoButton.ClickAsync();

        // After sign-in we land back on the home page — wait for the "User menu" button
        // which only appears in the authenticated layout (MainLayout.Authorized).
        // "Sign out" is now nested inside the collapsible user menu so it is not
        // visible by default; the toggle button is always visible once authenticated.
        // Playwright's Expect polls continuously across the navigation (demo-signin → / ),
        // so a generous timeout is sufficient; no separate navigation wait is needed.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Take and save the screenshot.
        // AppContext.BaseDirectory is e.g. bin/Debug/net10.0/ — navigate up three levels
        // to reach the test project root so that CI can find the file at
        // tests/TodoExtended.E2E/screenshots/ as configured in the upload-artifact step.
        var screenshotsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "screenshots"));
        Directory.CreateDirectory(screenshotsDir);
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Combine(screenshotsDir, "home-authenticated.png"),
            FullPage = true,
        });
    }
}
