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
        // Navigate directly to the demo sign-in endpoint rather than finding and clicking the
        // button on the home page.  The endpoint sets the auth cookie and issues a 302 redirect
        // to "/", so GotoAsync follows the redirect automatically.
        //
        // Using WaitUntilState.DOMContentLoaded avoids waiting for external resources such as
        // Google Fonts (fonts.googleapis.com) which can take 25+ seconds on a cold Android
        // emulator.  The User menu button comes from the server-side pre-rendered HTML, so it
        // is already in the DOM when DOMContentLoaded fires.
        await Page.GotoAsync($"{BaseUrl}/auth/demo-signin",
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        // After sign-in we land back on the home page — wait for the "User menu" button
        // which only appears in the authenticated layout (MainLayout.Authorized).
        // 30 s gives the Blazor interactive circuit enough time to hydrate on a slow
        // Android emulator while still failing fast on genuine errors.
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
