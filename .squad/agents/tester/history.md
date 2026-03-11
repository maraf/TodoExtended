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

### bUnit Tests for TaskListSkeleton & TaskStatsBar (2026-03-11)

- **New test files:** `TaskListSkeletonTests.cs` (7 tests), `TaskStatsBarTests.cs` (8 tests) — all 15 passing
- **TaskListSkeleton patterns:** Uses `.task-row` CSS class for row counting, `.w-16` for badge skeleton detection, `animate-pulse` for animation verification, `.card` + `dark:` for card wrapper assertions
- **TaskStatsBar patterns:** Empty markup check via `cut.Markup.Trim()` for zero-count case, `.chip-success` for completed chip detection, `button.TextContent` for toggle label text, `EventCallback<bool>` tested by capturing callback value
- **Proactive approach worked again:** Tests written from spec before verifying components existed; components were already in place and all tests passed first try
- **TaskStatsBar toggle pattern:** The component uses `!HideCompleted` inversion in the callback — test verifies `HideCompleted=false` produces callback value `true`

### bUnit Tests for Shared Components (2026-03-11)

- **Test project:** `tests/TodoExtended.Components.Tests/` — New bUnit + xUnit test project targeting net10.0
- **Components tested:** 6 shared Blazor components being extracted by Frontend:
  1. `ModalDialog.razor` — 7 tests (visibility, title, close callback, body/footer RenderFragments)
  2. `PageHeader.razor` — 5 tests (title in h1, gradient classes, icon rendering)
  3. `ErrorAlert.razor` — 6 tests (null/empty handling, rose styling, ⚠ prefix)
  4. `EmptyState.razor` — 7 tests (emoji, heading, description, action button + callback)
  5. `SkeletonGrid.razor` — 7 tests (count, height, animate-pulse, grid layout)
  6. `FloatingField.razor` — 7 tests (label, value binding, type, ValueChanged event)
- **Proactive approach:** Tests written before components exist — expected build errors until Frontend creates the actual components
- **Test patterns:** All tests follow Arrange-Act-Assert pattern with descriptive names (`MethodName_Scenario_ExpectedResult`)
- **bUnit version:** 1.32.7 (auto-resolved from 1.31.4 request)
- **Key file paths:**
  - Test project: `tests/TodoExtended.Components.Tests/TodoExtended.Components.Tests.csproj`
  - Test files: `*Tests.cs` (one per component)
  - Expected component location: `src/TodoExtended.Web/Components/Shared/*.razor`
- **README:** Created comprehensive test documentation at `tests/TodoExtended.Components.Tests/README.md` with expected parameters and test coverage
- **Integration notes:** Tests focus on component contracts (parameters → output) not exact CSS classes, may need minor adjustments when Frontend finalizes APIs

## Session: bUnit Test Suite for Shared Components (2026-03-11T08:33Z)

**Outcome 39 bUnit tests written; all passing:** 

### Test Coverage
| Component | Tests | Status |
|-----------|-------|--------|
| ModalDialog | 7 passing | | 
| PageHeader | 5 passing | | 
| ErrorAlert | 6 passing | | 
| EmptyState | 7 passing | | 
| SkeletonGrid | 7 passing | | 
| FloatingField | 7 passing | | 
| **Total** | **39 all passing** |** | **

### Test Project Details
- **Location:** `tests/TodoExtended.Components.Tests/`
- **Framework:** bUnit 1.32.7 + xUnit, net10.0
- **Pattern:** Contract-focused Arrange-Act-Assert
- **Documentation:** Comprehensive README.md with expected parameters

### Coordination Outcome
Coordinator resolved 8 API mismatches during Frontend implementation:
 `IsNullOrWhiteSpace`
2. PageHeader SectionContent: Rewrote tests for API limitation
3. EmptyState: Added OnAction parameter
4. FloatingField: Changed from onchange to oninput event

### Proactive Testing Benefits
- Tests defined component APIs before Frontend implementation
- Immediate feedback on deviations from specification
- Contracts serve as executable documentation
- Zero breaking changes to existing codebase

### Build Status
- Project builds clean
- All 39 tests passing
- Ready for production validation

### Dependencies
- Frontend delivered 6 components matching test specifications
- Coordinator aligned APIs across both teams


## 2026-03-11T08:51: Component Tests Archive

**Scribe Session:** Documented test work in orchestration log

### Artifacts Created

1. `.squad/orchestration-log/20260311T0851-tester. Test spawn manifest and coverage detailsmd` 

### Test Results Summary

- **TaskListSkeletonTests. 7 tests all passingcs** 
- **TaskStatsBarTests. 8 tests all passingcs** 
- **Total New  15Tests** 
- **Overall  54 tests passing (39 existing + 15 new)Suite** 

### Coverage Highlights

- Component rendering with default and custom parameters
- Two-way binding (@bind-HideCompleted) verification
- Null collection and zero-count edge cases
- Event callback assertions for toggle functionality

### AI Chat Service Tests (2026-03-11)

- **New test project:** `tests/TodoExtended.Tests/` — First unit test project for service-layer testing (separate from bUnit component tests)
- **Test files:** `ChatServiceTests.cs` (13 tests), `StubChatServiceTests.cs` (6 tests) — all 19 passing
- **Testing approach:** Write tests against INTERFACES using mocks, not implementations (Backend still building real ChatService)
- **Test patterns:** 
  - `SendMessageAsync` tests: validation, history handling, cancellation token respect
  - `ExecuteActionsAsync` tests: CreateTask, CompleteTask, UncompleteTask flows, rejection, mixed confirmations, error handling
  - StubChatService tests: verify placeholder returns "not configured" message, empty results
- **Mocking:** NSubstitute 5.3.0 for ITodoService mocks
- **Key learnings:**
  - Use `Task.FromException<T>()` for NSubstitute exception mocking, not `.ThrowsAsync()`
  - Use `Assert.ThrowsAnyAsync<OperationCanceledException>()` for cancellation tests (accepts TaskCanceledException subclass)
  - Test helper classes embedded in test file for contract validation (TestChatService, SlowChatService)
- **Project structure:** 
  - `tests/TodoExtended.Tests/` for unit tests
  - `tests/TodoExtended.Components.Tests/` for bUnit component tests
  - `tests/TodoExtended.E2E/` for Playwright E2E tests

