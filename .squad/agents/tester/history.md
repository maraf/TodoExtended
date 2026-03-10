# Tester History

<!-- Session logs appended by Scribe -->

## Infrastructure

### Playwright Screenshot Capture Infrastructure (2026-03-10)

- **Test file:** `tests/TodoExtended.E2E/ScreenshotCaptureTest.cs` — Automated screenshot capture system now available for all future documentation refreshes
- **Capabilities:** Single NUnit test iterates all 7 views × 2 themes × 2 viewports = 28 screenshots automatically
- **Output:** `docs/screenshots/` for full documentation set, `wwwroot/screenshots/` for PWA manifest 4 dark-theme screenshots
- **Reusability:** No manual screenshot updates needed — re-run test on code changes to refresh all 28 images

## Learnings

### Screenshot Capture E2E Test (2026-03-10)

- **Test file:** `tests/TodoExtended.E2E/ScreenshotCaptureTest.cs` — single Playwright NUnit test that captures 28 viewport screenshots (7 views × 2 themes × 2 viewports).
- **Pattern:** Sign in via demo mode ("Try Demo" link), navigate to each page, toggle theme via `button[aria-label='Toggle dark mode']`, set viewport, screenshot.
- **Key selectors:** Dark mode root is `div.dark` on outermost wrapper. Theme toggle uses `aria-label="Toggle dark mode"`. Templates dialog is `div.fixed.inset-0.z-50`.
- **Tasks page quirk:** `/tasks` with no ListId shows "Pick a list" (h2), not the tasks heading directly — use a combined CSS selector `h1:has-text('Tasks'), h2:has-text('Pick a list')`.
- **Templates dialog & Blazor re-render:** Toggling theme while dialog is open causes Blazor Server re-render that can close the dialog. Solution: set theme BEFORE opening the dialog, navigate fresh for each combination.
- **Playwright .NET API:** The wait state enum is `WaitForSelectorState` (not `WaitElementState`). Target framework is `net10.0`.
- **Screenshot output:** `docs/screenshots/` (repo root) for all 28 screenshots. 4 dark-theme screenshots also copied to `src/TodoExtended.Web/wwwroot/screenshots/` for PWA manifest.
- **Manifest:** `wwwroot/manifest.json` screenshots array references 4 files with sizes matching viewport dimensions exactly (1280x800 desktop, 390x844 mobile).
- **Running:** App must be started with `Demo__Enabled=true` env var on `http://localhost:5000`. Install Playwright browsers via `pwsh .../playwright.ps1 install chromium`.

### Screenshot DB Isolation (2026-03-10)

- **Isolated DB:** Use `ConnectionStrings__DefaultConnection=Data Source=../../artifacts/todoextended-screenshots.db` env var to avoid polluting the real user database with demo data cached by `CachedTodoService`.
- **Environment:** Must use `ASPNETCORE_ENVIRONMENT=Development` (not a custom environment) — `MapStaticAssets()` serves 0-byte CSS files in non-Development environments, producing unstyled screenshots.
- **Launch profile:** Use `dotnet run --no-launch-profile --urls http://localhost:5000` to prevent `launchSettings.json` from overriding env vars.
- **Dark mode toggle:** Blazor Server's `@onclick` handler requires the WebSocket circuit to be connected, which can take several seconds after page load. The test now toggles dark mode via direct JavaScript DOM manipulation (`root.classList.add('dark')`) instead of clicking the Blazor toggle button — reliable for screenshot purposes.
- **Home page selector:** The authenticated home page shows `<h3>No templates yet</h3>` (not `<h2>` or `<p>`), so the wait selector was updated to `h2:has-text('Quick Create'), h3:has-text('No templates yet')`.
- **Cleanup:** Always delete `artifacts/todoextended-screenshots.db*` after the test run to avoid stale data.
