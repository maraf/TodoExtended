# Frontend History

<!-- Session logs appended by Scribe -->

## Infrastructure

### Playwright Screenshot Capture Infrastructure (2026-03-10)

- **Test file:** `tests/TodoExtended.E2E/ScreenshotCaptureTest.cs` — Automated E2E screenshot capture system integrated
- **Pattern:** All 7 frontend views × 2 themes × 2 viewports = 28 screenshots automatically refreshed on each test run
- **Documentation:** `docs/screenshots/` contains full documentation screenshots. 4 dark-theme screenshots synced to `wwwroot/screenshots/` for PWA manifest
- **DB Isolation:** Uses `ConnectionStrings__DefaultConnection=Data Source=../../artifacts/todoextended-screenshots.db` environment variable override to prevent polluting local developer database with demo data
- **Impact:** Frontend design changes can now be automatically documented without manual screenshot steps

## 2026-03-10: Favicon PNG Export

**Session:** Favicon PNG Export (2026-03-10T10:49:00Z)

### Completed Tasks

- ✅ **Favicon generation** — Converted `TodoExtended_icon.svg` to PNG favicons (48×48, 192×192, 512×512) using SkiaSharp
- ✅ **Manifest update** — Updated `manifest.json` with PWA icon entries and metadata

### Outcome

All favicon sizes generated successfully. App now has multi-resolution PNG icons for full PWA compliance across mobile and desktop platforms.

### Files Modified

- `wwwroot/favicon.png` (new)
- `wwwroot/icon-192x192.png` (new)
- `wwwroot/icon-512x512.png` (new)
- `wwwroot/manifest.json` (updated)

---

## 2026-03-06: API Keys Card Redesign + MainLayout Fix

**Session:** API Keys Redesign (2026-03-06T10:14:01Z)

### Completed Tasks

- ✅ **ApiKeys.razor** — Card-based layout with MudDialog CRUD, responsive grid (3 cols desktop, 1 mobile)
- ✅ **MainLayout.razor** — Added `margin-top: var(--mud-appbar-height)` to MudMainContent to prevent heading overlap under sticky appbar

### Key Design Patterns

- **Responsive Card Grid:** MudGrid with breakpoint-based column counts
- **Dialog-driven CRUD:** "New API Key" button opens MudDialog for creation
- **Empty State:** Dashed border with VpnKey icon + CTA button (matches Templates pattern)
- **Card Actions:** Three-dot MudMenu for revoke action
- **Snackbar Feedback:** Success notification on key creation
- **Alert Positioning:** New-key alert moved outside loading/error conditional (always visible)

### Files Modified

- `src/TodoExtended.Web/Components/Pages/ApiKeys.razor`
- `src/TodoExtended.Web/Components/Layout/MainLayout.razor`

### Build Status

✅ Zero errors, zero warnings

## Core Context

### Bootstrap → Flowbite → MudBlazor Evolution

The codebase underwent two major UI framework migrations:

1. **2025-07-22: Bootstrap → Flowbite Blazor + Tailwind CSS**
   - 8 components redesigned (MainLayout, NavMenu, Home, Today, Tasks, Templates, ApiKeys, TaskStatusCheckbox)
   - Flowbite v0.2.6-beta components with Tailwind styling and dark mode support
   - Key learnings: Use native HTML inputs for reliable `@bind`, static imports for nested enums, `<Alert>` with custom content for dismissible alerts

2. **2026-03-06: Flowbite → MudBlazor v9**
   - All 8 components redesigned to Material Design using MudBlazor v9
   - Key patterns: `MudLayout` + `MudDrawer` (responsive), `MudDataGrid`/`MudTable` for lists, `ISnackbar` for non-intrusive feedback, custom `MudTheme` with blue/purple/teal palette
   - MudBlazor v9 specifics: `MudList<T>`, `MudDataGrid<T>`, `MudCheckBox<bool>` require type parameters; `ShowMessageBoxAsync` (not `ShowMessageBox`); `MudTabs` use `Class` not `PanelClass`

### Archive/Unarchive & Collapsible Sections

Added in parallel with sync performance improvements (2026-03-06):
- Archive/unarchive buttons on list items with spinner feedback
- Collapsible "Archived" section in sidebar with lazy-load via `GetArchivedTaskListsAsync()`
- Bootstrap Icons integration (11.11.3) from jsDelivr CDN

### Established Patterns

- Pages use `@page`, `@attribute [Authorize]`, `@rendermode InteractiveServer`, `@inject ITodoService`
- Loading/error/empty states via `_loading`, `_error` bools with conditional rendering
- Auth redirects on MSAL challenge via `NavigationManager.NavigateTo()`
- Optimistic UI updates with rollback on error (e.g. task toggle, list operations)
- `PersistentComponentState` for avoiding double-fetch during prerender→interactive transition
- `TaskStatusCheckbox` shared component encapsulates checkbox/spinner UI + API call + error handling

---

## 2026-03-06: Flowbite Blazor Migration Complete

**Session:** Flowbite Blazor UI Redesign (2026-03-06T09:33Z)

All 8 UI component files redesigned from Bootstrap to Flowbite Blazor components + Tailwind CSS. Dark mode support throughout. Build clean.

### Completed Tasks

- ✅ Redesigned MainLayout.razor with Flowbite Sidebar + responsive Tailwind grid
- ✅ Redesigned NavMenu.razor with Flowbite List, icons, and active link styling
- ✅ Redesigned Home.razor with Flowbite Card, Button, Heading, Spinner, Alert
- ✅ Redesigned Today.razor with Flowbite components + Card-styled task list
- ✅ Redesigned Tasks.razor with Flowbite dropdown + Card-styled lists per list
- ✅ Redesigned Templates.razor with Flowbite form components
- ✅ Redesigned ApiKeys.razor with Flowbite TextInput, Button, code block styling
- ✅ Updated TaskStatusCheckbox.razor with Flowbite Checkbox + Spinner UI
- ✅ Updated _Imports.razor with 4 Flowbite namespace imports + 2 static enum imports

### Key Decisions

1. **Native HTML inputs** — Used `<input>`, `<select>`, `<checkbox>` with Tailwind styling instead of Flowbite form components for better `@bind` reliability
2. **Static enum imports** — Added `@using static Flowbite.Components.Badge` and `@using static Flowbite.Components.Button` for nested type access
3. **Card-styled divs** — Raw Tailwind card styling (`bg-white rounded-lg border shadow-sm divide-y`) for list containers instead of `<Card>` component
4. **Dark mode throughout** — All custom Tailwind includes `dark:` variants

### Cross-Team Coordination

**Backend:** Infrastructure setup (NuGet, services, Tailwind CDN, cleanup) completed simultaneously. Breaking change from Bootstrap removal mitigated by coordinated Frontend redesign. Both agents succeeded in parallel.

### Technical Details

- Flowbite components: Sidebar, Card, Button, Table, Badge, Spinner, Alert, Heading, Paragraph, EmptyState, Icon
- Tailwind utility classes with responsive and dark mode variants
- Zero Bootstrap class references remain in codebase
- All 8 files follow updated patterns: auth gating, loading/error states, component composition

### Build Status

✅ Zero errors, zero warnings, clean build

## 2026-03-06: MudBlazor UI Redesign

**Session:** MudBlazor UI Redesign (2026-03-06T09:53:24Z)

All 8 UI component files redesigned from Flowbite Blazor + Tailwind CSS to MudBlazor Material Design. Build clean, zero errors.

### Completed Tasks

- ✅ **MainLayout.razor** — MudLayout with MudAppBar, MudDrawer (responsive), dark mode toggle
- ✅ **NavMenu.razor** — MudNavMenu with MudNavLink items, auto-highlighted routes
- ✅ **Home.razor** — MudGrid + MudCard dashboard with Material Design palette
- ✅ **Today.razor** — MudList with task items, MudSnackbar feedback
- ✅ **Tasks.razor** — MudTable for task lists, archive/unarchive, collapsible archived section
- ✅ **Templates.razor** — MudDataGrid + MudDialog for CRUD, Material typography
- ✅ **ApiKeys.razor** — MudDataGrid + MudDialog for API key management
- ✅ **TaskStatusCheckbox.razor** — MudCheckBox with status binding, MudSnackbar feedback

### Key Design Patterns

- **Responsive Layout:** MudAppBar (sticky), MudDrawer (auto-collapses on mobile)
- **Material Components:** MudList, MudTable, MudDataGrid, MudDialog, MudFab, MudChip
- **Feedback:** ISnackbar for non-intrusive notifications (replaces inline alerts)
- **Loading States:** MudSkeleton for content-shaped placeholders
- **Theming:** Custom MudTheme with blue primary, purple secondary, teal success. Dark mode toggle built in.
- **Icons:** MudBlazor Material Icons (`Icons.Material.Filled.*`)

### Cross-Team Coordination

**Backend:** MudBlazor infrastructure ready (services, CSS/JS, providers in App.razor)  
**Architect:** Design proposal informed all component implementations

### Build Status

✅ Zero errors, zero warnings, all components compile successfully

## Cross-Team Coordination

**Backend:** Added `GetTodayTasksAsync(CancellationToken)` method to ITodoService returning `IEnumerable<TodoTaskWithList>`. Aggregates tasks from all lists with due date matching today; parses Graph DueDateTime via `DateTimeOffset.Parse()`, uses `DateOnly.FromDateTime()` for timezone-safe comparison.

## Learnings

[Consolidated into ## Core Context section above]

### Golden Icon Integration (2026)

- **Pattern:** App uses `TodoExtended_icon.svg` (golden yellow variant) as the brand icon across favicon, app bar logo, and landing page logo
- **Favicon:** `App.razor` uses SVG favicon (`type="image/svg+xml"`) pointing to `TodoExtended_icon.svg` instead of PNG
- **App bar logo:** `MainLayout.razor` renders `<img src="TodoExtended_icon.svg">` in the header bar (replaces `✓` text placeholder)
- **Landing page logo:** `Home.razor` uses same SVG `<img>` in the unauthenticated landing page nav bar
- **Key files:** `App.razor`, `MainLayout.razor`, `Home.razor`
- **Old icon:** `Microsoft_To-Do_icon.svg` (blue) remains in wwwroot but is no longer referenced

### NavMenu Emoji Icon Extraction (2026)

- **Pattern:** Use `StringInfo.GetTextElementEnumerator()` + `Rune` for Unicode-safe leading emoji extraction from display names
- **MudBlazor constraint:** `MudNavLink.Icon` only accepts SVG path strings (from `Icons.Material.*`), not Unicode text — render emoji as styled `<span>` in child content instead
- **CSS isolation:** Created `NavMenu.razor.css` with `.nav-emoji-icon` class (24px box, inline-flex, 1.25rem font) to visually match Material icon slot
- **Key files:** `NavMenu.razor`, `NavMenu.razor.css`
- **Emoji ranges covered:** SMP blocks (≥ U+1F000), Misc Symbols, Dingbats, Misc Technical, Geometric Shapes, Arrows

## 2026-03-06 → 2026-03-07: Unauthenticated Landing Page

**Session:** Landing Page Experience (2026-03-06)  
**Completed:** 2026-03-07T11:22:54Z (Scribed)

### Completed Tasks

- ✅ **MainLayout.razor** — Wrapped MudAppBar, MudDrawer, and MudMainContent in `<AuthorizeView>` `<Authorized>` block. For `<NotAuthorized>`, render only `@Body` directly (MudBlazor providers remain active for both states)
- ✅ **Home.razor** — Redesigned `<NotAuthorized>` section as a full landing page with hero section, prominent sign-in button, and feature cards

### Key Design Patterns

- **Full-page landing:** For unauthenticated users, entire app chrome (app bar, drawer, navigation) is hidden
- **Material Design landing page:** MudContainer with MaxWidth.Medium, centered layout, large CheckCircle icon, hero text, and prominent "Sign in with Microsoft" button
- **Feature highlights grid:** 4 cards (Today View, Task Templates, Quick Create, Garmin Watch) using MudGrid with responsive breakpoints (2x2 on desktop, stacked on mobile)
- **MudBlazor providers outside AuthorizeView:** MudThemeProvider, MudPopoverProvider, MudDialogProvider, and MudSnackbarProvider stay active for both auth states so landing page can use MudBlazor components
- **Typography hierarchy:** h2 for app title, h5 for tagline and section headings, body2 for descriptions
- **Consistent theming:** Landing page uses the same Material Design palette as the authenticated app

### Technical Details

- Sign-in URL: `MicrosoftIdentity/Account/SignIn`
- Landing page components: MudContainer, MudPaper, MudIcon, MudText, MudButton, MudDivider, MudGrid, MudItem, MudCard
- Icons: Login icon for sign-in button; CheckCircle for hero; Today, ContentCopy, FlashOn, Watch for features
- Layout: Full viewport height centering with `min-height: 100vh` and flexbox alignment
- All existing authenticated functionality remains intact

### Files Modified

- `src/TodoExtended.Web/Components/Layout/MainLayout.razor`
- `src/TodoExtended.Web/Components/Pages/Home.razor`

### Build Status

✅ Zero errors, zero warnings

## 2026-03-06: Sync Performance Improvements

**Session:** Sync Performance Integration (2026-03-06T0901Z)

### Completed Tasks

1. **Archive/Unarchive UI**
   - Added archive/unarchive buttons to each list item in sidebar
   - Uses `_archivingListId` state tracking to prevent double-clicks
   - Shows spinner on button during API call
   - Calls `SetTaskListArchivedAsync()` on backend
   - On success: list moves between active and archived sections
   - On error: dismissible alert displays error message

2. **Collapsible Archived Section**
   - New "Archived" section in sidebar below active lists
   - Lazy-loads archived lists on first expand via `GetArchivedTaskListsAsync()`
   - Uses `_showArchived` bool for toggle state
   - Uses `_archivedListsLoaded` bool to prevent redundant API calls
   - Chevron icon animates on collapse/expand
   - Visually distinct styling (muted text/icons)

3. **Bootstrap Icons Integration**
   - Added `bootstrap-icons@1.11.3` CSS from jsDelivr CDN to `App.razor`
   - Enables icon classes: `bi-archive`, `bi-arrow-counterclockwise`, `bi-chevron-up`, `bi-chevron-down`
   - Icons rendered via `<i class="bi bi-*"></i>` inline elements

### Cross-Team Coordination

**Backend:** Implemented `IsArchived` flag on `CachedTaskList` entity with filters in sync methods. Parallel `SyncTasksForListsInParallelAsync()` using `Task.WhenAll` + `SemaphoreSlim`. SQLite WAL mode enabled at startup.

### Technical Details

- Optimistic list manipulation: after API success, lists are moved locally (no full reload)
- Selection clearing: when archiving the currently selected list, selection and tasks are cleared
- Component state: `_archivedLists` list tracks archived items separately from `TaskLists`
- Error handling: 401 redirects to OIDC sign-in via `NavigationManager.NavigateTo()`
- Lazy-load pattern prevents unnecessary API calls on page load

### Files Modified

Pages: `Components/Pages/Tasks.razor`  
Layout: `Components/App.razor`

### Build Status

 Project builds clean. UI renders correctly with Bootstrap Icons.

## 2025-07-22: Flowbite Blazor UI Redesign

[Consolidated into ## Core Context section — Flowbite Blazor v0.2.6-beta patterns and learnings documented there]

## 2026-03-06: Templates Page Card-Based Redesign

**Session:** Templates Redesign (2026-03-06T10:06Z)

Redesigned Templates.razor from MudDataGrid to card-based layout with MudDialog CRUD interface.

### Completed Tasks

-  Card-based display (3 cols desktop, 1 mobile) inside MudGrid
-  Grouped templates by task list with section headers
-  MudDialog for add/edit (opens from "New Template" button or empty state CTA)
-  Empty state with icon + CTA ("Create Your First Template")
-  Snackbar feedback for create/update/delete operations

### Key Design Patterns

- **Responsive Grid:** `MudGrid` with `md:3` columns collapses to 1 on mobile
- **Dialog-driven CRUD:** Cleaner than always-visible form
- **Empty State:** Better UX than generic alert
- **Grouped Display:** Section headers show task list context
- **Snackbar Feedback:** Non-intrusive success/error messaging

### Build Status

 Zero errors, zero warnings

## 2026-03-10: Header Layout Restructure

**Session:** Header Layout Restructure (2026-03-10)

### Completed Tasks

- ✅ **MainLayout.razor** — Restructured header to split into two sections (sidebar-width + main). Fixed height with flexbox layout. Header bar spans full width with gradient. Sidebar and main content scroll independently.
- ✅ **All pages** — Moved page icon + title from page body into header using `SectionContent` → `SectionOutlet` pattern (Tasks, Today, Templates, ApiKeys, SyncSettings, Home)

### Key Design Patterns

- **Split header layout:** Left section (w-64 on desktop) contains app logo "To Do (ex)" aligned above sidebar. Right section (flex-1) contains page icon + title, then user controls (user pill, dark mode toggle, sign out)
- **SectionContent/SectionOutlet:** Blazor's section pattern (available since .NET 8) used to pass page header content from individual pages to layout. Required `@using Microsoft.AspNetCore.Components.Sections` in `_Imports.razor`
- **Fixed header, scrollable content:** Outer container is `h-screen overflow-hidden flex flex-col`. Header is fixed height (h-14). Sidebar and main content are in a flex row with `overflow-y-auto` on each independently
- **Responsive icon hiding:** Page icons use `hidden sm:flex` to hide on mobile (< sm breakpoint), showing only text title
- **Gradient header:** Full-width gradient `bg-gradient-to-r from-brand-700 via-brand-600 to-violet-600` spans both sidebar and main sections

### Technical Details

- Page icons moved from `w-12 h-12 rounded-2xl` (in page body) to `w-10 h-10 rounded-xl` (in header) with `shadow-lg shadow-{color}-500/20`
- Page titles moved from `text-3xl font-extrabold` (h1 in body) to `text-lg font-bold text-white` (h1 in header)
- Each page defines `<SectionContent SectionName="page-header">` with icon + title. Layout renders `<SectionOutlet SectionName="page-header" />`
- Mobile: Hamburger menu + logo show inline (< lg breakpoint). Sidebar becomes overlay with `fixed` positioning and translate transform
- Desktop: Logo section shows as fixed `w-64` block in header with border-right separator

### Files Modified

- `src/TodoExtended.Web/Components/Layout/MainLayout.razor`
- `src/TodoExtended.Web/Components/_Imports.razor`
- `src/TodoExtended.Web/Components/Pages/Tasks.razor`
- `src/TodoExtended.Web/Components/Pages/Today.razor`
- `src/TodoExtended.Web/Components/Pages/Templates.razor`
- `src/TodoExtended.Web/Components/Pages/ApiKeys.razor`
- `src/TodoExtended.Web/Components/Pages/SyncSettings.razor`
- `src/TodoExtended.Web/Components/Pages/Home.razor`

### Build Status

✅ Zero errors, zero warnings

## 2026-03-XX: PWA Window Controls Overlay (WCO)

**Session:** PWA Window Controls Overlay Implementation

### Completed Tasks

- ✅ **manifest.json** — Added `"display_override": ["window-controls-overlay"]` to opt in to WCO API
- ✅ **App.razor** — Added `<meta name="theme-color" content="#4338ca">` for consistent PWA theming
- ✅ **MainLayout.razor** — Added `.wco-header` class on header, `.wco-no-drag` class on all interactive elements (buttons, links, divs)
- ✅ **tailwind-input.css** — Added WCO media query with `app-region: drag` on header, `app-region: no-drag` on interactive elements, `padding-top: env(titlebar-area-y)` to push content below window controls

### Key Design Patterns

- **Window Controls Overlay:** When installed as PWA on desktop, the app's gradient header extends into the native title bar area, with OS window control buttons (minimize/maximize/close) overlaid on top
- **CSS Environment Variables:** Uses `env(titlebar-area-y)` to detect title bar height and add padding to push header content below the window controls
- **Draggable Regions:** Header is draggable by default (`app-region: drag`), allowing users to drag the window. Interactive elements (buttons, links) explicitly marked as `app-region: no-drag` to remain clickable
- **Progressive Enhancement:** WCO styles only apply when `@media (display-mode: window-controls-overlay)` matches. Normal standalone/browser modes unchanged
- **Vendor Prefixes:** Both `-webkit-app-region` and standard `app-region` CSS properties used for cross-browser compatibility

### Technical Details

- **manifest.json:** `display_override` array prioritizes WCO, falls back to `standalone` if browser doesn't support it
- **CSS media query:** `@media (display-mode: window-controls-overlay)` scopes all WCO-specific styling
- **Header padding:** `padding-top: env(titlebar-area-y, 0)` with 0 fallback for non-WCO modes
- **Interactive markers:** All buttons, links, and content containers in header marked with `.wco-no-drag` class to preserve click/tap behavior
- **Gradient preservation:** The gradient background (`from-brand-700 via-brand-600 to-violet-600`) extends seamlessly behind the window controls, creating native-looking integration

### Files Modified

- `src/TodoExtended.Web/wwwroot/manifest.json`
- `src/TodoExtended.Web/Components/App.razor`
- `src/TodoExtended.Web/Components/Layout/MainLayout.razor`
- `src/TodoExtended.Web/tailwind-input.css`

### Build Status

✅ Tailwind CSS rebuilt successfully (230ms)

## 2026-03-10: Sidebar Controls Migration

**Session:** Move sign out, dark mode, user pill from header to sidebar bottom

### Completed Tasks

- ✅ **Removed from header** — Removed user pill, dark mode toggle, and sign out link from the header's main section
- ✅ **Added to sidebar bottom** — Pinned user controls to bottom of sidebar using lex flex-col layout with lex-1 scrollable nav area
- ✅ **Sidebar bottom design** — User name with icon, dark mode toggle, and sign out link with separator border, styled with sidebar theme colors (	ext-slate-600 dark:text-slate-400)
- ✅ **Header simplified** — Main section now only contains SectionOutlet for page headers, flex spacer removed
- ✅ **WCO classes cleaned** — Removed wco-no-drag from moved elements (they no longer live in the draggable header region)
- ✅ **Tailwind CSS rebuilt** — No new utility classes needed beyond existing ones

### Files Modified

- src/TodoExtended.Web/Components/Layout/MainLayout.razor

### Build Status

✅ .NET build succeeded (0 errors), Tailwind CSS rebuilt (235ms)

---

## 2026-03-11: Shared Component Extraction Refactoring

**Session:** Extract Duplicated Markup into Shared Components

### Created Components (src/TodoExtended.Web/Components/Shared/)

1. **ModalDialog. Reusable modal with `Visible`, `Title`, `OnClose`, `Body` (RenderFragment), `Footer` (RenderFragment)razor** 
2. **PageHeader. Page header with gradient icon badge via `Title`, `Icon`, `FromColor`, `ToColor`, `ShadowColor` (pass full Tailwind class names for JIT compatibility)razor** 
3. **ErrorAlert. Rose error banner, renders nothing when `Message` is null/emptyrazor** 
4. **EmptyState. Empty state card with `Emoji`/`CustomIcon`, `Heading`, `Description`, `ActionLabel`, `OnAction`, `ActionIcon`, `Dashed`razor** 
5. **SkeletonGrid. Loading skeleton grid with `Count`, `Height`, `Columns` (all Tailwind class strings)razor** 
6. **FloatingField. Floating label text input with `Label`, `@bind-Value`, `Type`, `Class`razor** 

### Pages Updated

- **Templates. Uses ModalDialog, PageHeader, ErrorAlert, EmptyState, SkeletonGrid, FloatingFieldrazor** 
- **ApiKeys. Uses ModalDialog, PageHeader, ErrorAlert, EmptyState, SkeletonGrid, FloatingFieldrazor** 
- **Today. Uses PageHeader, ErrorAlert, EmptyState (with CustomIcon for circle icon)razor** 
- **Tasks. Uses PageHeader, ErrorAlert, EmptyStaterazor** 
- **SyncSettings. Uses PageHeader, ErrorAlertrazor** 
- **Home. Uses PageHeader, ErrorAlert, SkeletonGridrazor** 

### Decisions

- **Tailwind JIT safety:** PageHeader and SkeletonGrid accept full Tailwind class names (e.g. `from-amber-400`) as parameters so the scanner finds them as string literals in calling pages
- **TaskItemRow skipped:** Today and Tasks task rows differ too much (different models, different sub-content) to justify a shared component
- **StatusBadge/Chip skipped:** Just CSS class applications (`chip chip-info`), not worth a component wrapper
- **FloatingField limited to text inputs:** Number/time/select variants have different binding patterns; only plain text inputs extracted

### Build Status

 .NET build: 0 errors, 0        Tailwind CSS rebuilt successfullywarnings 

## Learnings

- Pass full Tailwind class names as component parameters (e.g. `FromColor="from-amber-400"`) rather than partial names (e.g. `amber-400`) to preserve JIT scanner compatibility
- Shared components live in `Components/Shared/` and are auto-imported via `_Imports.razor` namespace
- EmptyState `CustomIcon` RenderFragment allows completely different icon areas (e.g. Today's emerald circle vs simple emoji)
- ModalDialog Body content spacing is caller's responsibility (Templates wraps in `<div class="space-y-4">`)

## Session: Shared Component Extraction Coordination (2026-03-11T08:33Z)

**Outcome 6 shared components extracted; 6 pages refactored; build clean:** 

### Components Delivered
- ** Overlay overlay + backdrop + header/body/footerModalDialog** 
- ** Section header with gradient icon badge  PageHeader** 
- ** Conditional rose-colored error bannerErrorAlert** 
- ** Card with icon/heading/description/action buttonEmptyState** 
- ** Loading placeholder gridSkeletonGrid** 
- ** Floating-label text input with two-way bindingFloatingField** 

### Components Deferred
- ** Too divergent across pages to extractTaskItemRow** 
- ** CSS-only pattern; minimal duplication valueStatusBadge** 

### Key Metrics
- ~200 lines of markup duplication eliminated
- 6 pages updated to use components
- Build Clean: 
- Tests 39/39 passing (via Tester): 

### Coordination with Tester
Tester provided 39 bUnit tests written proactively against component specification. Coordinator identified 8 API mismatches and aligned component implementation. Final result: all tests passing.

### Key Decision
All Tailwind class parameters must be passed as complete class names (e.g., `from-amber-400`, not `amber-400`) to ensure JIT scanner detection in calling pages.

### Dependencies
- Tester delivered 39 passing bUnit tests
- Coordinator ensured API alignment


## 2026-03-11: TaskListSkeleton & TaskStatsBar Extraction

**Session:** Extract duplicated markup from Today.razor and Tasks.razor into shared components

### Created Components (src/TodoExtended.Web/Components/Shared/)

1. **TaskListSkeleton. Loading skeleton card with animated pulse rows. Parameters: `RowCount` (default 5), `GradientClasses` (default "from-violet-500 via-brand-500 to-sky-500"), `ShowBadgeSkeleton` (default false, adds extra badge placeholder)razor** 
2. **TaskStatsBar. Stats chips bar with open/done counts and hide/show completed toggle. Parameters: `OpenCount`, `CompletedCount`, `OpenLabel` (default "open"), `@bind-HideCompleted` (two-way binding via EventCallback). Component self-hides when total count is 0.razor** 

### Pages Updated

- **Today. Replaced inline stats bar (28 lines) with `<TaskStatsBar>`, replaced inline skeleton (12 lines) with `<TaskListSkeleton>`. Uses null-safe `?.Count() ?? 0` for nullable TodayTasks list.razor** 
- **Tasks. Same replacements. TaskListSkeleton uses all defaults; TaskStatsBar uses default "open" label.razor** 

### Learnings

- When extracting components that take computed values from nullable collections, use `?.Count() ?? 0` pattern to avoid CS8604 null reference warnings
- `@bind-HideCompleted` two-way binding requires matching `HideCompleted` parameter + `HideCompletedChanged` EventCallback<bool> on the component
- Components with internal `@if` guards (rendering nothing when no data) let callers skip wrapping  cleaner call sitesconditionals 

## 2026-03-11T08:51: Task List Component Extraction Complete

**Scribe Session:** Documented extraction work in logs and decisions

### Artifacts Created

1. `.squad/decisions. Added formal decision record for TaskListSkeleton & TaskStatsBarmd` 
2. `.squad/log/20260311T0851-task-list-component-extraction. Session summarymd` 
3. `.squad/orchestration-log/20260311T0851-frontend. Frontend spawn manifest detailsmd` 

### Impact

- Component extraction documented and decision archived
- 15 new bUnit tests covering both components
- ~70 lines of duplicated markup eliminated
- Both Today.razor and Tasks.razor build clean with `-warnaserror`


---

##  UX Pattern Library Audit (2026-03-11)Learnings 

### Task
Analyzed all 12 shared components, 4 layout components, and 8 pages to produce a comprehensive UX Pattern Library at `docs/ux-patterns.md`.

### Key UX Patterns Found

1. **12 shared components** documented: EmptyState, ErrorAlert, FloatingField, ModalDialog, NavItem, NavListItem, PageHeader, SkeletonGrid, TaskListSkeleton, TaskStatsBar, TaskStatusCheckbox, ToastStack
2. **17 pattern categories** identified: cards, task rows, loading skeletons, modals, error alerts, empty states, page headers, floating label inputs, nav items, stats bars, chips/badges (5 variants), buttons (6 variants), toggle switches, toast notifications, progress bars, gradient decorations, icon badges, tab navigation, spinners, section dividers, responsive grids
3. **CSS utility system** in `tailwind-input.css` defines `.card`, `.card-hover`, `.chip-*`, `.btn-*`, `.task-row`, `.floating-*`, `.section-title`, `.input`, `. all with dark mode variantslabel` 
4. **Brand color palette** is Indigo-based (`brand-50` through `brand-900`) with violet, amber, emerald, sky, rose supplementary gradients
5. **Page header pattern** uses `SectionContent`/`SectionOutlet` to pass page titles into the layout header bar
6. **Toast notification system** via `INotificationService` with 4 severity levels and auto-purge

### Document Path
`docs/ux-patterns.md`

---

## AI Chat UI (Issue #22)

### Components Created
- `src/TodoExtended.Web/Components/Pages/Chat.razor` — Main chat page (route: `/chat`, InteractiveServer)
- `src/TodoExtended.Web/Components/Shared/ChatInput.razor` — Text input with send button, Enter-to-submit, auto-focus
- `src/TodoExtended.Web/Components/Shared/ChatMessageBubble.razor` — Message display (user right/blue, assistant left/gray)
- `src/TodoExtended.Web/Components/Shared/ProposedActionCard.razor` — Action confirm/reject card with type-specific accent colors

### Files Modified
- `NavMenu.razor` — Added "AI Chat" link under TOOLS section
- `NavItem.razor` — Added "chat" icon (speech bubble SVG)
- `_Imports.razor` — Added `@using TodoExtended.Web.Services.AiChat` globally

## Learnings

### AI Chat Architecture Patterns
- Used `MessageEntry` wrapper class to track per-message state (confirmations, results) without mutating immutable records
- Chat conversation is ephemeral (in-memory `List<ChatMessage>`) — no persistence needed per spec
- Actions execute only after ALL proposed actions in a message are decided (approve/reject), to avoid partial execution confusion
- Auto-scroll via JS eval on `data-chat-scroll` attribute
- ProposedActionCard uses RenderFragment-returning methods for action-type icons (same pattern as NavItem.GetIcon)
- PageHeader component used with violet-to-fuchsia gradient for AI Chat branding
- Tailwind CSS MUST be rebuilt (`npm run build:css`) after adding new utility classes to Razor files

## 2026-03-11: AI Chat UI & Components (Squad #22)

**Status:** Complete

Built Chat.razor page and extracted reusable components per Marek's directive:

**Chat.razor Page:**
- Ephemeral in-memory conversation state
 results
- Injects IChatService and ITodoService
- MessageEntry wrapper tracks per-message state (confirmations, results)
- Batch action execution (all decisions before execution)

**Extracted Components:**
- `ChatInput. Message input with send callbackrazor` 
- `ChatMessageBubble. Message renderer (user vs AI styling)razor` 
- `ProposedActionCard. Action card with confirm/reject flowrazor` 

**Navigation:**
- NavMenu updated with AI Chat link (TOOLS section, first item)
- Routes to `/chat`

**Global Imports:**
- Added `@using TodoExtended.Web.Services.AiChat` to _Imports.razor

**Tailwind Rebuild:**
- Compiled CSS with chat utilities

**Decisions:**
- Ephemeral state (no persistence)
- Reusable component architecture
- Batch action execution to prevent partial execution
- Global using for AiChat namespace

** Clean (no errors/warnings)  Build:** 
** RebuiltTailwind:** 

**Orchestration Log:** .squad/orchestration-log/20260311T095047Z-frontend.md


## 2026-03-11: Template Action Types Support

**Status:** Complete

Updated `ProposedActionCard.razor` to handle 4 new template action types being added by Backend in parallel:

**New Enum Values:**
 "Create Template" label, violet theme, chip-info, document+plus icon
 "Update Template" label, violet theme, chip-info, pencil/edit icon
 "Delete Template" label, rose theme, chip-error, trash icon
 "Execute Template" label, cyan theme, chip-success, lightning icon

**Changes Made:**
1. ** Added display labels for all 4 new typesActionLabel()** 
2. ** Violet theme for Create/Update, rose for Delete, cyan for ExecuteAccentBorderClass()** 
3. ** Matching background colors with `/40` opacity for dark modeAccentBgClass()** 
4. ** Mapped to existing chip classes (chip-info, chip-error, chip-success)AccentChipClass()** 
5. ** SVG icons with theme-specific colors (violet-600, rose-600, cyan-600)ActionIcon()** 
6. **Display  Added `templateId` extraction (though not yet used in  future proofing)display Logic** 

**Available Chip Classes** (from tailwind-input.css):
- `chip-primary` (brand blue)
- `chip-success` (emerald)
- `chip-error` (rose)
- `chip-warning` (amber)
- `chip-info` (sky)

**Color Theme Strategy:**
- Template actions use distinct color schemes to visually differentiate from task actions
- Create/Update templates: violet/purple (intellectual/creative)
- Delete template: rose (destructive action)
- Execute template: cyan/teal (action/execution)

**Tailwind CSS:** Rebuilt via `npm run build:css` to ensure new color classes are compiled
**Build:** Verified clean (0 errors, 0 warnings)
