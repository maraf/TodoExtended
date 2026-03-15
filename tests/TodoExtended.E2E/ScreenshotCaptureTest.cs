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

        // Attempt templates-timepicker screenshots
        await CaptureTemplatesTimePickerScreenshots();

        // Capture sidebar-open on mobile
        await CaptureSidebarOpenScreenshots();

        // Capture chat screenshots
        await CaptureChatEmptyScreenshots();
        await CaptureChatWithMessagesScreenshots();
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

    private async Task CaptureTemplatesTimePickerScreenshots()
    {
        foreach (var theme in new[] { "dark", "light" })
        {
            var vp = new ViewportSpec("desktop", 1280, 800);
            try
            {
                await Page.SetViewportSizeAsync(vp.Width, vp.Height);
                await Page.GotoAsync($"{BaseUrl}/templates", new() { WaitUntil = WaitUntilState.NetworkIdle });
                await Page.Locator("h1:has-text('Templates')").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

                await SetThemeAsync(theme);
                await Page.WaitForTimeoutAsync(500);

                // Wait for Blazor circuit
                await Page.WaitForTimeoutAsync(3_000);

                // Open dialog
                var newButton = Page.Locator("button:has-text('New Template')");
                if (await newButton.CountAsync() == 0)
                {
                    TestContext.WriteLine("[WARN] No 'New Template' button — skipping timepicker screenshots.");
                    return;
                }
                await newButton.First.ClickAsync();

                var dialogLocator = Page.Locator("div.fixed.inset-0.z-50");
                await dialogLocator.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
                await Page.WaitForTimeoutAsync(500);

                // Enable "Due Today" toggle — click the label wrapper, not the sr-only input
                var dueTodayLabel = Page.Locator("label.cursor-pointer:has(input[type='checkbox'].sr-only)");
                await dueTodayLabel.First.ClickAsync();
                await Page.WaitForTimeoutAsync(300);

                // Click the time picker trigger to open the dropdown
                var timePickerButton = Page.Locator("button:has-text('Select time…')");
                if (await timePickerButton.CountAsync() > 0)
                {
                    await timePickerButton.First.ClickAsync();
                    await Page.WaitForTimeoutAsync(300);
                }

                var fileName = $"templates-timepicker--{vp.Name}-{theme}.png";
                await Page.ScreenshotAsync(new()
                {
                    Path = Path.Combine(ScreenshotsDir, fileName),
                });
                TestContext.WriteLine($"[OK] {fileName}");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"[WARN] Failed: templates-timepicker--{vp.Name}-{theme}: {ex.Message}");
            }
        }
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

    private async Task CaptureChatEmptyScreenshots()
    {
        foreach (var theme in new[] { "dark", "light" })
        {
            foreach (var vp in Viewports)
            {
                try
                {
                    await Page.SetViewportSizeAsync(vp.Width, vp.Height);

                    // Clear any existing chat history so the empty state is shown
                    await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });
                    await Page.EvaluateAsync("() => localStorage.removeItem('todoextended-chat-history')");
                    await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });

                    // Wait for empty state placeholder
                    await Page.Locator("h2:has-text('Chat with your tasks')").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

                    await SetThemeAsync(theme);
                    await Page.WaitForTimeoutAsync(500);

                    var fileName = $"chat-empty--{vp.Name}-{theme}.png";
                    await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotsDir, fileName) });
                    TestContext.WriteLine($"[OK] {fileName}");
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"[WARN] Failed: chat-empty--{vp.Name}-{theme}: {ex.Message}");
                }
            }
        }
    }

    private async Task CaptureChatWithMessagesScreenshots()
    {
        // A realistic multi-turn conversation long enough to trigger the scrollbar.
        // PropertyNameCaseInsensitive=true means any casing is fine for deserialization.
        const string chatHistory = """
            [
              {"message":{"role":"user","text":"Hey! What can you help me with?","proposedActions":null,"timestamp":"2026-01-10T09:00:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"I can help you manage your tasks! Here\u2019s what I can do:\n\n\u2022 Create new tasks in any of your task lists\n\u2022 Mark tasks as complete or reopen them\n\u2022 Create, update, or delete task templates\n\u2022 Execute templates to quickly add recurring tasks\n\nJust tell me what you\u2019d like \u2014 for example, \u201cAdd a task to buy milk\u201d or \u201cComplete my gym workout.\u201d","proposedActions":null,"timestamp":"2026-01-10T09:00:04+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"user","text":"Add a task to buy milk in my Shopping list","proposedActions":null,"timestamp":"2026-01-10T09:01:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"I\u2019ll create that task in your Shopping list:","proposedActions":[{"type":0,"description":"Create task in Shopping","parameters":{"title":"Buy milk","listId":"abc123","listName":"Shopping"}}],"timestamp":"2026-01-10T09:01:05+00:00","taskListReferences":[{"id":"abc123","displayName":"Shopping"}]},"results":[{"actionIndex":0,"success":true,"message":"Task created successfully"}]},
              {"message":{"role":"user","text":"Great! Now mark my morning run as complete.","proposedActions":null,"timestamp":"2026-01-10T09:02:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"Done! I\u2019ve marked your morning run as complete:","proposedActions":[{"type":1,"description":"Complete 'Morning Run'","parameters":{"title":"Morning Run","listId":"abc124","listName":"Exercise"}}],"timestamp":"2026-01-10T09:02:06+00:00","taskListReferences":[{"id":"abc124","displayName":"Exercise"}]},"results":[{"actionIndex":0,"success":true,"message":"Task completed"}]},
              {"message":{"role":"user","text":"Can you reopen the budget review task?","proposedActions":null,"timestamp":"2026-01-10T09:02:30+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"Sure, I\u2019ll reopen that for you:","proposedActions":[{"type":2,"description":"Reopen 'Budget Review'","parameters":{"title":"Budget Review","listId":"abc125","listName":"Work"}}],"timestamp":"2026-01-10T09:02:35+00:00","taskListReferences":[{"id":"abc125","displayName":"Work"}]},"results":[{"actionIndex":0,"success":true,"message":"Task reopened"}]},
              {"message":{"role":"user","text":"What are my task lists?","proposedActions":null,"timestamp":"2026-01-10T09:03:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"Here are your task lists:\n\n\u2022 Shopping \u2014 everyday errands\n\u2022 Exercise \u2014 fitness goals\n\u2022 Work \u2014 professional tasks\n\u2022 Personal \u2014 everything else\n\nTap any name to open the list directly.","proposedActions":null,"timestamp":"2026-01-10T09:03:04+00:00","taskListReferences":[{"id":"abc123","displayName":"Shopping"},{"id":"abc124","displayName":"Exercise"},{"id":"abc125","displayName":"Work"},{"id":"abc126","displayName":"Personal"}]},"results":null},
              {"message":{"role":"user","text":"Create a morning routine template","proposedActions":null,"timestamp":"2026-01-10T09:04:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"I\u2019ll set up a morning routine template:","proposedActions":[{"type":3,"description":"Create 'Morning Routine' template","parameters":{"title":"Morning Routine"}}],"timestamp":"2026-01-10T09:04:07+00:00","taskListReferences":null},"results":[{"actionIndex":0,"success":true,"message":"Template created"}]},
              {"message":{"role":"user","text":"Perfect, thanks! You\u2019re really helpful.","proposedActions":null,"timestamp":"2026-01-10T09:05:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"Happy to help! I\u2019m here whenever you need to manage your tasks. Just send me a message anytime.","proposedActions":null,"timestamp":"2026-01-10T09:05:03+00:00","taskListReferences":null},"results":null}
            ]
            """;

        foreach (var theme in new[] { "dark", "light" })
        {
            foreach (var vp in Viewports)
            {
                try
                {
                    await Page.SetViewportSizeAsync(vp.Width, vp.Height);

                    // Seed localStorage before navigating so the page loads with the conversation
                    await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });
                    await Page.EvaluateAsync("value => localStorage.setItem('todoextended-chat-history', value)", chatHistory);

                    // Reload so Blazor reads the seeded localStorage
                    await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });

                    // Wait for at least one message bubble to appear
                    await Page.Locator(".bg-brand-600, .bg-slate-100").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

                    await SetThemeAsync(theme);

                    // Scroll to bottom so the most recent messages are visible
                    await Page.EvaluateAsync("() => { const el = document.querySelector('main'); if (el) el.scrollTop = el.scrollHeight; }");
                    await Page.WaitForTimeoutAsync(500);

                    var fileName = $"chat-messages--{vp.Name}-{theme}.png";
                    await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotsDir, fileName) });
                    TestContext.WriteLine($"[OK] {fileName}");
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"[WARN] Failed: chat-messages--{vp.Name}-{theme}: {ex.Message}");
                }
            }
        }
    }
}
