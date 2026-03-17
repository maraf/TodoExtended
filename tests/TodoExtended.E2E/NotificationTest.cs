using Microsoft.Playwright;
using NUnit.Framework;

namespace TodoExtended.E2E;

/// <summary>
/// Verifies that toast notifications are correctly shown and do not overflow
/// the viewport on both mobile (390×844) and desktop (1280×800).
/// Requires the app running with Demo__Enabled=true.
/// </summary>
[TestFixture]
public class NotificationTest : E2ETestBase
{

    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static string ScreenshotsDir => Path.Combine(RepoRoot, "docs", "screenshots");

    [Test]
    public async Task Notification_IsVisibleAndDoesNotOverflow_OnMobileViewport()
    {
        await AssertNotificationDoesNotOverflowAsync(width: 390, height: 844, screenshotName: "notification--mobile-light.png");
    }

    [Test]
    public async Task Notification_IsVisibleAndDoesNotOverflow_OnDesktopViewport()
    {
        await AssertNotificationDoesNotOverflowAsync(width: 1280, height: 800, screenshotName: "notification--desktop-light.png");
    }

    private async Task AssertNotificationDoesNotOverflowAsync(int width, int height, string screenshotName)
    {
        Directory.CreateDirectory(ScreenshotsDir);

        await Page.SetViewportSizeAsync(width, height);

        // Sign in via demo — navigate directly to the endpoint so the cookie is set
        // even when the browser already has an authenticated session from a previous test.
        await Page.GotoAsync($"{BaseUrl}/auth/demo-signin", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        // Navigate to home page and wait for content
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the Blazor circuit to connect
        await Page.WaitForTimeoutAsync(3_000);

        // Check if Quick Create templates are available in demo mode
        var quickCreateHeader = Page.Locator("h2:has-text('Quick Create')");
        bool hasQuickCreate = await quickCreateHeader.CountAsync() > 0
                           && await quickCreateHeader.IsVisibleAsync();

        if (!hasQuickCreate)
        {
            Assert.Inconclusive("No Quick Create templates available in demo mode — cannot trigger notification.");
            return;
        }

        // Click the first "Create Task" button — this triggers a success notification
        var createTaskButton = Page.Locator("button:has-text('Create Task')").First;
        await createTaskButton.ClickAsync();

        // Wait for the toast notification to appear
        var notification = Page.Locator("[role='status']").First;
        await notification.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });

        await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotsDir, screenshotName) });

        // Verify the notification is fully within the viewport (no overflow)
        var viewportSize = Page.ViewportSize!;
        var box = await notification.BoundingBoxAsync();

        Assert.That(box, Is.Not.Null, "Notification element should have a bounding box");
        Assert.That(box!.X, Is.GreaterThanOrEqualTo(0),
            $"Notification left edge ({box.X:F1}px) should not overflow the left of the viewport");
        Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(viewportSize.Width),
            $"Notification right edge ({box.X + box.Width:F1}px) should not exceed viewport width ({viewportSize.Width}px)");

        TestContext.WriteLine($"[OK] Notification at x={box.X:F1}px, width={box.Width:F1}px on {viewportSize.Width}px viewport — no overflow");
    }
}
