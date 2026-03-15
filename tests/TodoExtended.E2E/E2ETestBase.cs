using Microsoft.Playwright;
using NUnit.Framework;

namespace TodoExtended.E2E;

/// <summary>
/// Base class for all E2E tests.
/// When <c>E2E_USE_ANDROID_EMULATOR=true</c>, connects to Chrome running on the
/// Android emulator via the CDP endpoint specified by <c>E2E_ANDROID_CDP_ENDPOINT</c>
/// (default: <c>http://localhost:9222</c>) and defaults the base URL to
/// <c>http://10.0.2.2:5000</c> (the Android emulator's alias for the host loopback).
/// Otherwise, launches a headless Playwright Chromium browser and defaults the base
/// URL to <c>http://localhost:5000</c>.
/// Override <see cref="ContextOptions"/> to customise the browser context.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public abstract class E2ETestBase
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    protected IPage Page { get; private set; } = null!;

    /// <summary>
    /// App base URL used by all tests.
    /// Resolved from <c>E2E_BASE_URL</c> env var, or automatically set to the
    /// Android emulator host address when <c>E2E_USE_ANDROID_EMULATOR=true</c>.
    /// </summary>
    protected string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL")
        ?? (UseAndroidEmulator ? "http://10.0.2.2:5000" : "http://localhost:5000");

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
            // Connect to Chrome already running on the Android emulator via CDP.
            // The caller is responsible for starting Chrome with remote debugging
            // enabled and forwarding the CDP port (adb forward tcp:9222
            // localabstract:chrome_devtools_remote) before the tests run.
            _browser = await _playwright.Chromium.ConnectOverCDPAsync(AndroidCdpEndpoint);
        }
        else
        {
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });
        }

        _context = await _browser.NewContextAsync(ContextOptions());
        Page = await _context.NewPageAsync();
    }

    [TearDown]
    public async Task BaseTearDownAsync()
    {
        if (_context != null) await _context.CloseAsync();
        // Closing a CDP-connected browser only disconnects; it does not kill Chrome on the device.
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    /// <summary>Override to supply custom <see cref="BrowserNewContextOptions"/>.</summary>
    protected virtual BrowserNewContextOptions ContextOptions() => new();

    protected static ILocatorAssertions Expect(ILocator locator) =>
        Assertions.Expect(locator);

    protected static IPageAssertions Expect(IPage page) =>
        Assertions.Expect(page);
}
