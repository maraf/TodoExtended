using Microsoft.Playwright;
using NUnit.Framework;

namespace TodoExtended.E2E;

/// <summary>
/// Captures viewport screenshots of every app view across two themes (light/dark)
/// and two viewports (desktop 1280×800, mobile 390×844).
/// Output lands in docs/screenshots/ at the repo root.
/// Requires the app running with Demo__Enabled=true.
/// </summary>
[TestFixture]
public class ScreenshotCaptureTest : E2ETestBase
{

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
        await CaptureChatKeyboardOpenScreenshots();
        await CaptureChatSetReminderScreenshots();
        await CaptureChatActionCardsScreenshots();
    }

    private async Task SignInViaDemoAsync()
    {
        await Page.GotoAsync(BaseUrl);

        var demoButton = Page.Locator("a[href='/auth/demo-signin']").First;
        await Expect(demoButton).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await demoButton.ClickAsync();

        // Wait for authenticated state — "User menu" button is always visible in the sidebar
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
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

    private async Task CaptureChatKeyboardOpenScreenshots()
    {
        // Simulate a mobile phone where the soft keyboard occupies ~300 px of the 844 px screen,
        // leaving 540 px of visible viewport for the app (interactive-widget=resizes-content).
        // We add the 'keyboard-open' body class via JS to activate the CSS rules that hide the
        // bottom bar and remove the reserved padding-bottom, then capture the result.
        const string chatHistory = """
            [
              {"message":{"role":"user","text":"What can you help me with?","proposedActions":null,"timestamp":"2026-01-10T09:00:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"I can help you manage your tasks! I can create tasks, complete them, reopen them, and work with your task templates. Just tell me what you need.","proposedActions":null,"timestamp":"2026-01-10T09:00:04+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"user","text":"Add a task to buy milk in my Shopping list","proposedActions":null,"timestamp":"2026-01-10T09:01:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"I\u2019ll create that task in your Shopping list:","proposedActions":[{"type":0,"description":"Create task in Shopping","parameters":{"title":"Buy milk","listId":"abc123","listName":"Shopping"}}],"timestamp":"2026-01-10T09:01:05+00:00","taskListReferences":[{"id":"abc123","displayName":"Shopping"}]},"results":[{"actionIndex":0,"success":true,"message":"Task created successfully"}]}
            ]
            """;

        var mobileVp = new ViewportSpec("mobile", 390, 540); // ~844 − 304 px keyboard

        foreach (var theme in new[] { "dark", "light" })
        {
            try
            {
                await Page.SetViewportSizeAsync(mobileVp.Width, mobileVp.Height);

                // Seed localStorage before navigating so the page loads with the conversation
                await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });
                await Page.EvaluateAsync("value => localStorage.setItem('todoextended-chat-history', value)", chatHistory);

                // Reload so Blazor reads the seeded localStorage
                await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });

                // Wait for at least one message bubble to appear
                await Page.Locator(".bg-brand-600, .bg-slate-100").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

                await SetThemeAsync(theme);

                // Simulate keyboard open: add body class that hides the bottom bar and resets padding
                await Page.EvaluateAsync("() => document.body.classList.add('keyboard-open')");

                // Scroll to bottom so the input bar is visible
                await Page.EvaluateAsync("() => { const el = document.querySelector('main'); if (el) el.scrollTop = el.scrollHeight; }");
                await Page.WaitForTimeoutAsync(500);

                var fileName = $"chat-keyboard-open--{mobileVp.Name}-{theme}.png";
                await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotsDir, fileName) });
                TestContext.WriteLine($"[OK] {fileName}");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"[WARN] Failed: chat-keyboard-open--{mobileVp.Name}-{theme}: {ex.Message}");
            }
        }
    }

    private async Task CaptureChatSetReminderScreenshots()
    {
        // Verifies that the ProposedActionCard renders the Set Reminder action correctly:
        // listName and reminderDate are provided inline in the seeded history (as ChatService would
        // produce after enrichment), so this screenshot validates UI rendering of those fields.
        const string chatHistory = """
            [
              {"message":{"role":"user","text":"Set a reminder on \"Build a barricade\" for tomorrow at 16:00","proposedActions":null,"timestamp":"2026-03-16T17:00:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"I am about to set a reminder for the task \"Build a barricade\" tomorrow at 16:00. Should I proceed?","proposedActions":[{"type":3,"description":"Set reminder on task \"Build a barricade\" at 16:00","parameters":{"taskTitle":"Build a barricade","listId":"demo-list-personal","listName":"\ud83c\udfe0 Personal","reminderDate":"2026-03-17","reminderTime":"16:00","taskId":"demo-task-barricade"}}],"timestamp":"2026-03-16T17:00:03+00:00","taskListReferences":null},"results":[{"actionIndex":0,"success":true,"message":"SetReminder completed successfully."}]}
            ]
            """;

        foreach (var theme in new[] { "dark", "light" })
        {
            foreach (var vp in new[] { new ViewportSpec("mobile", 390, 844), new ViewportSpec("desktop", 1280, 800) })
            {
                try
                {
                    await Page.SetViewportSizeAsync(vp.Width, vp.Height);

                    await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });
                    await Page.EvaluateAsync("value => localStorage.setItem('todoextended-chat-history', value)", chatHistory);

                    await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });

                    await Page.Locator(".bg-brand-600, .bg-slate-100").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

                    await SetThemeAsync(theme);

                    await Page.EvaluateAsync("() => { const el = document.querySelector('main'); if (el) el.scrollTop = el.scrollHeight; }");
                    await Page.WaitForTimeoutAsync(500);

                    var fileName = $"chat-set-reminder--{vp.Name}-{theme}.png";
                    await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotsDir, fileName) });
                    TestContext.WriteLine($"[OK] {fileName}");
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"[WARN] Failed: chat-set-reminder--{vp.Name}-{theme}: {ex.Message}");
                }
            }
        }
    }

    private async Task CaptureChatActionCardsScreenshots()
    {
        // Verifies that Create, Complete, and Reopen action cards all display the resolved
        // task list name (e.g. "📋 Work") instead of the "Task list" fallback.
        // The seeded history simulates what ChatService produces after EnrichListNameAsync runs.
        const string chatHistory = """
            [
              {"message":{"role":"user","text":"Please create a task 'Prepare report', complete 'Send email' and reopen 'Review PR'","proposedActions":null,"timestamp":"2026-03-17T10:00:00+00:00","taskListReferences":null},"results":null},
              {"message":{"role":"assistant","text":"Sure! Here are the three actions for your confirmation:","proposedActions":[{"type":0,"description":"Create task \"Prepare report\"","parameters":{"title":"Prepare report","listId":"demo-list-work","listName":"📋 Work"}},{"type":1,"description":"Complete task \"Send email\"","parameters":{"title":"Send email","listId":"demo-list-work","listName":"📋 Work","taskId":"demo-task-w1"}},{"type":2,"description":"Reopen task \"Review PR\"","parameters":{"title":"Review PR","listId":"demo-list-personal","listName":"🏠 Personal","taskId":"demo-task-p1"}}],"timestamp":"2026-03-17T10:00:05+00:00","taskListReferences":null},"results":null}
            ]
            """;

        foreach (var theme in new[] { "dark", "light" })
        {
            foreach (var vp in Viewports)
            {
                try
                {
                    await Page.SetViewportSizeAsync(vp.Width, vp.Height);

                    await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });
                    await Page.EvaluateAsync("value => localStorage.setItem('todoextended-chat-history', value)", chatHistory);

                    await Page.GotoAsync($"{BaseUrl}/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });

                    await Page.Locator(".bg-emerald-600, .border-emerald-200").First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

                    await SetThemeAsync(theme);
                    await Page.WaitForTimeoutAsync(500);

                    var fileName = $"chat-action-cards--{vp.Name}-{theme}.png";
                    await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotsDir, fileName) });
                    TestContext.WriteLine($"[OK] {fileName}");
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"[WARN] Failed: chat-action-cards--{vp.Name}-{theme}: {ex.Message}");
                }
            }
        }
    }
}
