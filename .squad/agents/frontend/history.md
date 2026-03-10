# Frontend History

<!-- Session logs appended by Scribe -->

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
