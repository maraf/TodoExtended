using Microsoft.Playwright;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace TodoExtended.E2E;

/// <summary>
/// Playwright E2E test verifying that an unauthenticated user who navigates directly
/// to a protected route is redirected to the landing page.
/// Requires the app to be running with Demo:Enabled=true.
/// Set the E2E_BASE_URL environment variable to the app base URL (default: http://localhost:5000).
/// </summary>
[TestFixture]
public class UnauthenticatedAccessTest : E2ETestBase
{
    [Test]
    public async Task NavigateToProtectedRoute_WhenUnauthenticated_RedirectsToLandingPage()
    {
        // Ensure we are signed out before the test.
        await Page.GotoAsync($"{BaseUrl}/auth/demo-signout", new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        // Navigate directly to a protected route without signing in.
        await Page.GotoAsync($"{BaseUrl}/today");

        // The app should redirect to the landing page (/) and show the sign-in link.
        var signInLink = Page.Locator("a[href='MicrosoftIdentity/Account/SignIn']").First;
        await Expect(signInLink).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Confirm we are on the landing page URL.
        await Expect(Page).ToHaveURLAsync(new Regex($"^{Regex.Escape(BaseUrl)}/?$"), new() { Timeout = 5_000 });
    }
}
