# Decision: Screenshot DB Isolation for E2E Tests

**Date:** 2026-03-10  
**Author:** Tester  
**Status:** Implemented

## Context

The Playwright screenshot capture test (`ScreenshotCaptureTest.cs`) runs the app in demo mode, which uses `CachedTodoService` to cache demo data into SQLite. Without isolation, this pollutes the developer's local `todoextended.db` with demo task lists and tasks.

## Decision

Use a **separate SQLite database** for screenshot capture via the `ConnectionStrings__DefaultConnection` environment variable:

```
ConnectionStrings__DefaultConnection=Data Source=../../artifacts/todoextended-screenshots.db
```

**Critical constraints discovered:**
1. **Must use `Development` environment** — `MapStaticAssets()` serves 0-byte CSS files in non-Development environments, producing unstyled screenshots
2. **Must use `--no-launch-profile`** — `launchSettings.json` overrides `ASPNETCORE_ENVIRONMENT` when using `dotnet run`
3. **Must clean up after** — delete `todoextended-screenshots.db*` to avoid stale cached demo data

## Dark Mode Toggle

The Blazor Server `@onclick` handler for theme toggling requires a connected WebSocket circuit, which takes several seconds after page load. The test now uses **direct JavaScript DOM manipulation** (`root.classList.add('dark')`) instead of clicking the Blazor toggle button. This is reliable for screenshot purposes since only the visual state matters.

## Impact

- Modified: `tests/TodoExtended.E2E/ScreenshotCaptureTest.cs` — JS-based theme toggle, updated home page selector
- No production code changes required
