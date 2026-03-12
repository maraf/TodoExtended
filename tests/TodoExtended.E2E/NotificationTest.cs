using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace TodoExtended.E2E;

/// <summary>
/// Verifies that toast notifications are correctly shown and do not overflow
/// the viewport on mobile view (390×844).
/// Requires the app running with Demo__Enabled=true.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class NotificationTest : PageTest
{
    private string BaseUrl => Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5000";

    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static string ScreenshotsDir => Path.Combine(RepoRoot, "docs", "screenshots");

    [Test]
    public async Task Notification_IsVisibleAndDoesNotOverflow_OnMobileViewport()
    {
        Directory.CreateDirectory(ScreenshotsDir);

        // Use mobile viewport (390×844, matching iPhone 14 Pro)
        await Page.SetViewportSizeAsync(390, 844);

        // Sign in via demo
        await Page.GotoAsync(BaseUrl);
        var demoButton = Page.GetByRole(AriaRole.Link, new() { Name = "Try Demo" });
        await Expect(demoButton).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await demoButton.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Navigate to home page and wait for content
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the page to settle and the Blazor circuit to connect
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

        // Capture screenshot showing the notification on mobile
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Combine(ScreenshotsDir, "notification--mobile-light.png"),
        });

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

    [Test]
    public async Task Notification_IsVisibleAndDoesNotOverflow_OnDesktopViewport()
    {
        Directory.CreateDirectory(ScreenshotsDir);

        // Use desktop viewport (1280×800)
        await Page.SetViewportSizeAsync(1280, 800);

        // Sign in via demo
        await Page.GotoAsync(BaseUrl);
        var demoButton = Page.GetByRole(AriaRole.Link, new() { Name = "Try Demo" });
        await Expect(demoButton).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await demoButton.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Navigate to home page and wait for content
        await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the page to settle and the Blazor circuit to connect
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

        // Capture screenshot showing the notification on desktop
        await Page.ScreenshotAsync(new()
        {
            Path = Path.Combine(ScreenshotsDir, "notification--desktop-light.png"),
        });

        // Verify the notification is fully within the viewport
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
