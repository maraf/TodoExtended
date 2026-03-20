using Microsoft.Playwright;
using NUnit.Framework;

namespace TodoExtended.E2E;

/// <summary>
/// Base class for all E2E tests.
/// When <c>E2E_USE_ANDROID_EMULATOR=true</c>, connects to Chrome running on the
/// Android emulator via the CDP endpoint specified by <c>E2E_ANDROID_CDP_ENDPOINT</c>
/// (default: <c>http://localhost:9222</c>) and reuses the existing page opened by
/// the launch script.  The workflow sets up <c>adb reverse tcp:5000 tcp:5000</c> so
/// that Chrome on the emulator can reach the host app at <c>localhost:5000</c>.
/// Otherwise, launches a headless Playwright Chromium browser and defaults the base
/// URL to <c>http://localhost:5000</c>.
/// Override <see cref="ContextOptions"/> to customise the browser context.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public abstract class E2ETestBase
{
    // Android Chrome only accepts one CDP connection at a time. Serialise all
    // Android-mode test setups/teardowns to prevent concurrent CDP conflicts.
    private static readonly SemaphoreSlim _androidCdpLock = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private bool _contextOwned;
    private bool _androidCdpLockHeld;

    protected IPage Page { get; private set; } = null!;

    /// <summary>
    /// App base URL used by all tests.
    /// Resolved from <c>E2E_BASE_URL</c> env var, or defaults to
    /// <c>http://localhost:5000</c>.  In Android emulator mode, the CI workflow
    /// uses <c>adb reverse tcp:5000 tcp:5000</c> so that Chrome on the emulator
    /// can reach the host app at <c>localhost:5000</c>.
    /// </summary>
    protected string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL")
        ?? "http://localhost:5000";

    /// <summary>True when <c>E2E_USE_ANDROID_EMULATOR=true</c> is set.</summary>
    protected static bool UseAndroidEmulator =>
        string.Equals(
            Environment.GetEnvironmentVariable("E2E_USE_ANDROID_EMULATOR"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static string AndroidCdpEndpoint =>
        Environment.GetEnvironmentVariable("E2E_ANDROID_CDP_ENDPOINT") ?? "http://localhost:9222";

    [SetUp]
    public async Task BaseSetUpAsync()
    {
        _playwright = await Playwright.CreateAsync();

        if (UseAndroidEmulator)
        {
            // Serialise Android CDP access: Chrome on Android only supports one CDP
            // connection at a time; concurrent connections cause internal Playwright
            // errors. The lock is released in BaseTearDownAsync.
            await _androidCdpLock.WaitAsync();
            _androidCdpLockHeld = true;
            try
            {
                // Connect to Chrome already running on the Android emulator via CDP.
                // The caller is responsible for starting Chrome with remote debugging
                // enabled and forwarding the CDP port (adb forward tcp:9222
                // localabstract:chrome_devtools_remote) before the tests run.
                _browser = await _playwright.Chromium.ConnectOverCDPAsync(AndroidCdpEndpoint);
                // Android Chrome does not support Target.createBrowserContext, so we
                // reuse the default context that already exists on the connected browser.
                _context = _browser.Contexts[0];
                _contextOwned = false;
                // Reuse the existing open tab rather than creating a new one.
                // Android Chrome does not support Target.createTarget (used internally
                // by NewPageAsync), so we must reuse the about:blank page that Chrome
                // opened when it was launched by the script.
                if (_context.Pages.Count == 0)
                    throw new InvalidOperationException(
                        "Android Chrome has no open pages to reuse; " +
                        "ensure Chrome is launched to 'about:blank' before the tests run.");
                Page = _context.Pages[0];
            }
            catch
            {
                _androidCdpLockHeld = false;
                _androidCdpLock.Release();
                throw;
            }
            return;
        }

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
        _context = await _browser.NewContextAsync(ContextOptions());
        _contextOwned = true;
        Page = await _context.NewPageAsync();
    }

    [TearDown]
    public async Task BaseTearDownAsync()
    {
        try
        {
            if (_contextOwned)
            {
                // Closing the context also closes all pages within it.
                if (_context != null) await _context.CloseAsync();
            }
            else
            {
                // We don't own the default CDP context (Android mode). Don't close the
                // page — that would leave Chrome with no open tab, causing the next test
                // to fail with Pages.Count == 0. Instead navigate back to about:blank so
                // the tab is clean for the next test.
                if (Page != null) await Page.GotoAsync("about:blank");
            }
            // Closing a CDP-connected browser only disconnects; it does not kill Chrome on the device.
            if (_browser != null) await _browser.CloseAsync();
            _playwright?.Dispose();
        }
        finally
        {
            if (_androidCdpLockHeld)
            {
                _androidCdpLockHeld = false;
                _androidCdpLock.Release();
            }
        }
    }

    /// <summary>Override to supply custom <see cref="BrowserNewContextOptions"/>.</summary>
    protected virtual BrowserNewContextOptions ContextOptions() => new();

    /// <summary>
    /// Sets the Tailwind dark-mode class on the page's root element via JavaScript.
    /// Works without requiring the Blazor Server circuit to be connected.
    /// </summary>
    protected async Task SetThemeAsync(string theme)
    {
        bool wantDark = theme == "dark";
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

    protected static ILocatorAssertions Expect(ILocator locator) =>
        Assertions.Expect(locator);

    protected static IPageAssertions Expect(IPage page) =>
        Assertions.Expect(page);
}
