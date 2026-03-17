using Microsoft.Playwright;
using NUnit.Framework;

namespace TodoExtended.E2E;

/// <summary>
/// Playwright E2E test that signs in using the demo account and takes a screenshot.
/// Requires the app to be running with Demo:Enabled=true.
/// Set the E2E_BASE_URL environment variable to the app base URL (default: http://localhost:5000).
/// </summary>
[TestFixture]
public class DemoSignInTest : E2ETestBase
{

    [Test]
    public async Task SignInAsDemo_ShowsAuthenticatedHomePage_AndTakesScreenshot()
    {
        // Navigate to the home page (unauthenticated) and sign in via the demo button.
        await Page.GotoAsync(BaseUrl);

        // Locate the demo sign-in link by its stable route rather than its display text.
        var demoButton = Page.Locator("a[href='/auth/demo-signin']").First;
        await Expect(demoButton).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Click the demo sign-in link — data-enhance-nav="false" on the button forces a real
        // browser navigation (not Blazor's fetch-based enhanced nav), ensuring the Set-Cookie
        // header from /auth/demo-signin is stored before / is loaded.
        await demoButton.ClickAsync();

        // After sign-in we land back on the home page — wait for the "User menu" button
        // which only appears in the authenticated layout (MainLayout.Authorized).
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

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
