# UX Pattern Library — TodoExtended

> **Last updated:** 2026-03-11
> **Maintained by:** Frontend

---

## Guidelines

1. **Always check this list** before creating new UI. Reuse existing patterns.
2. If a new pattern is genuinely needed, **add it to this document first**, then implement.
3. Every custom class includes `dark:` variants — never ship light-only styles.
4. Use the **brand color palette** (Indigo-based) defined in `tailwind.config.js`:

| Token | Hex | Usage |
|---|---|---|
| `brand-50` | `#eef2ff` | Subtle backgrounds, chip fills |
| `brand-100` | `#e0e7ff` | Icon badge backgrounds |
| `brand-400` | `#818cf8` | Dark-mode accent text |
| `brand-500` | `#6366f1` | Progress bars, gradient midpoints |
| `brand-600` | `#4f46e5` | Primary buttons, links, focus rings |
| `brand-700` | `#4338ca` | Header gradient, hover states |

**Supplementary gradients:** violet, purple, amber/orange, emerald/teal, sky/blue, rose, fuchsia.

**Dark mode:** Toggled via `class` strategy on root `<div>`. Backgrounds use `slate-800`/`slate-900`; borders use `slate-700`.

**Font:** Inter (with system fallback).

**Animations:** `animate-fade-in`, `animate-slide-in`, `animate-spin-slow`, `animate-pulse` (Tailwind built-in).

---

## 1. Layout & Navigation

### 1.1 App Shell / Main Layout

| | |
|---|---|
| **Component** | `Layout/MainLayout.razor` |
| **Structure** | Fixed header (`h-14`) + sidebar (`w-64`) + scrollable main content. `h-screen overflow-hidden flex flex-col`. |
| **Header** | Gradient `bg-gradient-to-r from-brand-700 via-brand-600 to-violet-600`. Left: logo section (desktop, `w-64`). Right: `<SectionOutlet SectionName="page-header" />`. |
| **Sidebar** | White/`slate-800` panel, `overflow-y-auto`, slides in on mobile (`translate-x` transition). Contains `<NavMenu />` + user controls footer. |
| **Main** | `flex-1 min-w-0 overflow-y-auto`, content wrapped in `max-w-6xl mx-auto px-4 sm:px-6 py-6 sm:py-8`. |
| **Mobile** | Overlay backdrop (`bg-black/50`) when sidebar open. `<BottomBar />` fixed at bottom. |
| **Used on** | Every authenticated page |

### 1.2 Page Header

| | |
|---|---|
| **Component** | `Shared/PageHeader.razor` |
| **Visual** | Renders into layout header via `<SectionContent SectionName="page-header">`. Gradient icon badge (10×10, rounded-xl) + bold white title. Icon hidden on mobile (`hidden sm:flex`). |
| **Parameters** | `Title` (string), `Icon` (RenderFragment), `FromColor`, `ToColor`, `ShadowColor` |
| **Used on** | Home, Tasks, Today, Templates, ApiKeys, SyncSettings |

```razor
<PageHeader Title="Today" FromColor="from-amber-400" ToColor="to-orange-500" ShadowColor="shadow-amber-500/20">
    <Icon>
        <svg class="w-5 h-5 text-white" ...>...</svg>
    </Icon>
</PageHeader>
```

### 1.3 Nav Item (Icon Link)

| | |
|---|---|
| **Component** | `Shared/NavItem.razor` |
| **Visual** | `mx-2 px-3 py-2.5 rounded-xl` sidebar link with a 5×5 SVG icon slot. Active state: `bg-brand-50 text-brand-700 font-semibold`. Hover: `bg-slate-100`. |
| **Parameters** | `Href`, `Match` (NavLinkMatch), `Icon` (string key: grid, sun, copy, key, refresh, list), `ChildContent` |
| **Behavior** | Cascading `CloseSidebar` action closes mobile sidebar on click. |
| **Used on** | NavMenu (Home, Today, Templates, API Keys, Sync Settings) |

```razor
<NavItem Href="/today" Match="NavLinkMatch.Prefix" Icon="sun">Today</NavItem>
```

### 1.4 Nav List Item (Emoji Link)

| | |
|---|---|
| **Component** | `Shared/NavListItem.razor` |
| **Visual** | Similar to NavItem but renders an emoji prefix (or fallback list SVG). Slightly smaller padding (`py-2`). Text truncates. |
| **Parameters** | `Href`, `Emoji` (string?), `ChildContent` |
| **Used on** | NavMenu — dynamic task list links |

```razor
<NavListItem Href="/tasks/abc123" Emoji="🐶">Domeczech</NavListItem>
```

### 1.5 Section Title (Sidebar)

| | |
|---|---|
| **Component** | Inline markup (CSS class `section-title`) |
| **Visual** | `text-xs font-bold uppercase tracking-widest text-slate-400 px-3 mb-1` |
| **Used on** | NavMenu ("MAIN", "TOOLS", "MY LISTS", "SETTINGS") |

```razor
<p class="section-title mt-5">TOOLS</p>
```

### 1.6 Bottom Bar (Mobile)

| | |
|---|---|
| **Component** | `Layout/BottomBar.razor` |
| **Visual** | Fixed bottom nav, `lg:hidden`. White bar with shadow, flex row of icon buttons (hamburger, Today, Templates). |
| **Parameters** | `OnToggleSidebar` (EventCallback) |
| **Used on** | MainLayout (mobile only) |

---

## 2. Cards & Containers

### 2.1 Card

| | |
|---|---|
| **Component** | CSS class `.card` |
| **Visual** | `bg-white rounded-2xl border border-slate-100 shadow-sm` / `dark:bg-slate-800 dark:border-slate-700` |
| **Used on** | Task list containers (Tasks, Today), SyncSettings list, EmptyState wrapper |

```razor
<div class="card dark:bg-slate-800 dark:border-slate-700 overflow-hidden">
    <!-- content -->
</div>
```

### 2.2 Card Hover (Interactive Card)

| | |
|---|---|
| **Component** | CSS class `.card-hover` |
| **Visual** | Extends `.card` + `transition-all duration-150 hover:shadow-md` |
| **Used on** | Template cards (Home, Templates), API key cards |

```razor
<div class="card-hover dark:bg-slate-800 dark:border-slate-700 p-5">
    <!-- card content -->
</div>
```

### 2.3 Landing Feature Card

| | |
|---|---|
| **Component** | Inline markup (Home.razor, NotAuthorized section) |
| **Visual** | `bg-white/5 hover:bg-white/10 backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:-translate-y-1`. Dark glass-morphism style against `slate-950` background. |
| **Used on** | Home page landing (unauthenticated) |

---

## 3. Lists & Task Rows

### 3.1 Task Row

| | |
|---|---|
| **Component** | CSS class `.task-row` |
| **Visual** | `flex items-center gap-3 px-5 py-4 hover:bg-slate-50 dark:hover:bg-slate-700/50`. Adjacent rows get a `border-t border-slate-100 dark:border-slate-700` separator. |
| **Used on** | Tasks, Today, SyncSettings |

```razor
<div class="task-row @(task.IsCompleted ? "opacity-50" : "")">
    <TaskStatusCheckbox ... />
    <span class="flex-1 min-w-0 text-sm font-medium truncate">@task.Title</span>
    <span class="chip chip-error">!</span>
</div>
```

### 3.2 Task Status Checkbox

| | |
|---|---|
| **Component** | `Shared/TaskStatusCheckbox.razor` |
| **Visual** | 5×5 rounded checkbox (`accent-brand-600`). While toggling, shows a brand-colored spinner. |
| **Parameters** | `TaskId`, `ListId`, `IsCompleted`, `TaskTitle`, `OnStatusChanged` (EventCallback\<bool\>), `OnError` (EventCallback\<string\>) |
| **Behavior** | Optimistic UI update via `OnStatusChanged`, then calls `TodoService.UpdateTaskStatusAsync`. Reverts on error. MSAL consent redirect on auth challenge. |
| **Used on** | Tasks, Today |

```razor
<TaskStatusCheckbox TaskId="@task.Id" ListId="@ListId"
                    IsCompleted="@task.IsCompleted" TaskTitle="@task.Title"
                    OnStatusChanged="(s) => HandleStatus(task.Id, s)"
                    OnError="(msg) => Notifications.Add(msg, NotifySeverity.Warning)" />
```

---

## 4. Loading & Skeletons

### 4.1 Task List Skeleton

| | |
|---|---|
| **Component** | `Shared/TaskListSkeleton.razor` |
| **Visual** | Card with gradient top bar (`h-1 animate-pulse`) + rows of pulsing placeholders (checkbox square + text bar + optional badge). |
| **Parameters** | `RowCount` (default 5), `GradientClasses` (default `from-violet-500 via-brand-500 to-sky-500`), `ShowBadgeSkeleton` (bool) |
| **Used on** | Tasks, Today |

```razor
<TaskListSkeleton RowCount="4"
                  GradientClasses="from-brand-500 via-violet-500 to-fuchsia-500"
                  ShowBadgeSkeleton="true" />
```

### 4.2 Skeleton Grid

| | |
|---|---|
| **Component** | `Shared/SkeletonGrid.razor` |
| **Visual** | Responsive grid of pulsing rounded-2xl rectangles (`bg-slate-100 dark:bg-slate-800 animate-pulse`). |
| **Parameters** | `Count` (default 3), `Height` (Tailwind class, default `h-32`), `Columns` (default `grid-cols-1 sm:grid-cols-2 lg:grid-cols-3`) |
| **Used on** | Home, Templates, ApiKeys |

```razor
<SkeletonGrid Height="h-40" />
```

### 4.3 Inline Skeleton (SyncSettings)

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | Card with gradient top bar + rows of `h-4 bg-slate-100 rounded-full animate-pulse` bars inside `.task-row`. |
| **Used on** | SyncSettings (both synced and not-synced tabs) |

---

## 5. Feedback & Notifications

### 5.1 Error Alert

| | |
|---|---|
| **Component** | `Shared/ErrorAlert.razor` |
| **Visual** | `rounded-2xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-800 text-rose-700 dark:text-rose-400 px-5 py-4 text-sm font-medium`. Prefixed with ⚠ emoji. |
| **Parameters** | `Message` (string?) — renders nothing if null/empty |
| **Used on** | Home, Tasks, Today, Templates, ApiKeys, SyncSettings |

```razor
<ErrorAlert Message="@_error" />
```

### 5.2 Toast Stack

| | |
|---|---|
| **Component** | `Shared/ToastStack.razor` |
| **Visual** | Fixed bottom-right (`fixed bottom-4 right-4 z-50`), max-w-sm column of toast items. Each toast is a rounded-2xl pill with severity-based colors, emoji icon, message text, and dismiss button. `animate-fade-in`. |
| **Severities** | Success (emerald/✅), Error (rose/❌), Warning (amber/⚠️), Info (sky/ℹ️) |
| **Behavior** | Subscribes to `INotificationService.Changed`. Auto-purges expired toasts every 1 second. `aria-live` set to `assertive` for error/warning, `polite` for others. |
| **Used on** | MainLayout (global, always rendered) |

```csharp
// From any page/component:
Notifications.Add("Task created!", NotifySeverity.Success);
```

### 5.3 Inline Form Error

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | `rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-800 text-rose-700 dark:text-rose-400 px-4 py-3 text-sm` |
| **Used on** | Templates modal form, ApiKeys modal form |

### 5.4 Warning Banner (API Key Created)

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | `rounded-2xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800`. Contains 🔑 icon, heading, description, readonly input + copy button. Dismissible. |
| **Used on** | ApiKeys (after key creation) |

### 5.5 Reconnect Modal

| | |
|---|---|
| **Component** | `Layout/ReconnectModal.razor` |
| **Visual** | Native `<dialog>` element with `bg-white dark:bg-slate-800 rounded-2xl shadow-2xl`. Pulsing brand dot animation, status messages for various reconnection states. Retry/Resume buttons using `btn-primary`. |
| **Used on** | App.razor (Blazor framework integration) |

---

## 6. Empty States

### 6.1 Empty State

| | |
|---|---|
| **Component** | `Shared/EmptyState.razor` |
| **Visual** | Card container centered vertically (`py-20`), with large emoji or custom icon, heading, description, and optional CTA button. |
| **Parameters** | `Emoji` (string), `CustomIcon` (RenderFragment), `Heading`, `Description`, `ActionLabel`, `OnAction` (EventCallback), `ActionIcon` (RenderFragment), `Dashed` (bool — adds `border-2 border-dashed`) |
| **Variants** | Dashed border for "create first" prompts. Custom icon for richer visuals (e.g., Today's checkmark circle). |
| **Used on** | Tasks (empty list), Today (all caught up), Templates (no templates), ApiKeys (no keys) |

```razor
<!-- Simple emoji style -->
<EmptyState Emoji="✅" Heading="Empty list" Description="No tasks in this list yet." />

<!-- With CTA button -->
<EmptyState Emoji="📝" Heading="No templates yet"
            Description="Create templates to quickly add recurring tasks."
            ActionLabel="Create First Template" OnAction="OpenAddDialog" Dashed="true">
    <ActionIcon>
        <svg class="w-4 h-4" ...><!-- plus icon --></svg>
    </ActionIcon>
</EmptyState>

<!-- Custom icon (Today) -->
<EmptyState Heading="All caught up! 🎉" Description="No tasks due today.">
    <CustomIcon>
        <div class="w-16 h-16 rounded-full bg-emerald-100 ...">
            <svg class="w-8 h-8 text-emerald-500" .../>
        </div>
    </CustomIcon>
</EmptyState>
```

### 6.2 Inline Empty State

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | Centered text within a card: emoji + heading + action link (e.g., "All tasks completed!" + "Show completed tasks"). |
| **Used on** | Tasks, Today (when all visible tasks are completed but completed tasks are hidden) |

---

## 7. Progress & Stats

### 7.1 Task Stats Bar

| | |
|---|---|
| **Component** | `Shared/TaskStatsBar.razor` |
| **Visual** | Horizontal flex row with chips (open count, done count) and a "Hide/Show completed" toggle button with eye icon. `mb-6`. |
| **Parameters** | `OpenCount` (int), `CompletedCount` (int), `OpenLabel` (default "open"), `HideCompleted` + `HideCompletedChanged` (two-way binding) |
| **Behavior** | Self-hides when total is 0. Toggle changes eye icon between open/slashed. |
| **Used on** | Tasks, Today |

```razor
<TaskStatsBar OpenCount="@(tasks.Count(t => !t.IsCompleted))"
              CompletedCount="@(tasks.Count(t => t.IsCompleted))"
              OpenLabel="remaining"
              @bind-HideCompleted="_hideCompleted" />
```

### 7.2 Progress Bar

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | `h-1.5 bg-slate-100 dark:bg-slate-700` track with a gradient fill (`bg-gradient-to-r from-violet-500 to-brand-500 rounded-r-full transition-all duration-500`). Width set via percentage style. |
| **Used on** | Tasks (violet→brand), Today (brand→emerald) |

```razor
<div class="h-1.5 bg-slate-100 dark:bg-slate-700">
    <div class="h-full bg-gradient-to-r from-violet-500 to-brand-500 rounded-r-full transition-all duration-500"
         style="width: @(pct)%"></div>
</div>
```

### 7.3 Mini Progress Bar (Template Execution)

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | `h-1.5 bg-brand-100 rounded-full` track with `bg-brand-500 rounded-full animate-pulse w-2/3` fill. Indeterminate style. |
| **Used on** | Home (while executing a template) |

---

## 8. Chips & Badges

### 8.1 Chip Variants

Defined in `tailwind-input.css` as utility classes:

| Class | Colors | Usage |
|---|---|---|
| `.chip-primary` | `bg-brand-100 text-brand-700` | Open task count |
| `.chip-success` | `bg-emerald-100 text-emerald-700` | Done task count |
| `.chip-error` | `bg-rose-100 text-rose-700` | High priority indicator ("!") |
| `.chip-warning` | `bg-amber-100 text-amber-700` | "Due Today" badge on templates |
| `.chip-info` | `bg-sky-100 text-sky-700` | List name badge (Today), reminder time |

```razor
<span class="chip chip-primary">5 open</span>
<span class="chip chip-error" title="High priority">!</span>
<span class="chip chip-info text-xs hidden sm:inline-flex">@task.ListName</span>
```

### 8.2 Sort Order Badge

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | `absolute top-3 right-3 w-5 h-5 rounded-full bg-slate-100 dark:bg-slate-700 text-slate-500 text-xs font-bold flex items-center justify-center` |
| **Used on** | Templates (non-zero sort order indicator) |

---

## 9. Buttons

All button classes extend the base `.btn` class: `inline-flex items-center justify-center gap-2 font-semibold rounded-xl transition-all duration-150 focus:ring-2 focus:ring-offset-2 disabled:opacity-50`.

| Class | Visual | Usage |
|---|---|---|
| `.btn-primary` | Brand-600 bg, white text, `px-5 py-2.5 text-sm shadow-sm` | Primary actions: "Create", "Save", "New Template" |
| `.btn-outline` | Brand-600 border + text, transparent bg | Not currently used (available) |
| `.btn-ghost` | Transparent bg, slate-500 text, hover bg-slate-100, `px-3 py-2` | Cancel, edit, secondary actions |
| `.btn-danger` | Rose-600 bg, white text | Delete confirmations (available) |
| `.btn-sm` | Modifier: `px-3 py-1.5 text-xs rounded-lg` | Card-level actions: "Create", "Edit", "Copy" |
| `.btn-lg` | Modifier: `px-7 py-3.5 text-base rounded-2xl` | Landing CTA (available) |

### Spinner Button Pattern

When `_saving` / `_creating` is true, replace button label with inline spinner:

```razor
<button @onclick="Save" disabled="@_saving" class="btn-primary">
    @if (_saving)
    {
        <svg class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
    }
    Save
</button>
```

### Danger Ghost Button

Rose-tinted ghost for destructive actions:

```razor
<button class="btn-ghost btn-sm text-rose-500 hover:text-rose-700 hover:bg-rose-50 dark:hover:bg-rose-950/30">
    <!-- trash icon -->
</button>
```

---

## 10. Modal Dialogs

### 10.1 Modal Dialog

| | |
|---|---|
| **Component** | `Shared/ModalDialog.razor` |
| **Visual** | Fixed overlay (`bg-black/60 backdrop-blur-sm`) + centered `max-w-md rounded-3xl shadow-2xl` panel. Header with title + close button. Body slot. Optional footer with action buttons. `animate-fade-in`. |
| **Parameters** | `Visible` (bool), `Title`, `OnClose` (EventCallback), `Body` (RenderFragment), `Footer` (RenderFragment) |
| **Used on** | Templates (add/edit), ApiKeys (create) |

```razor
<ModalDialog Visible="_dialogVisible" Title="New Template" OnClose="CloseDialog">
    <Body>
        <FloatingField Label="Title" @bind-Value="_formTitle" />
    </Body>
    <Footer>
        <button @onclick="CloseDialog" class="btn-ghost">Cancel</button>
        <button @onclick="Save" class="btn-primary">Create</button>
    </Footer>
</ModalDialog>
```

---

## 11. Form Inputs

### 11.1 Floating Label Field

| | |
|---|---|
| **Component** | `Shared/FloatingField.razor` |
| **Visual** | `.floating-field` wrapper with `.floating-input` (rounded-xl, slate-50 bg, `pt-6 pb-2`) and `.floating-label` that animates from placeholder position to tiny top-left label on focus/fill. Label turns `brand-600` when active. |
| **Parameters** | `Label`, `Value` + `ValueChanged` (two-way), `Type` (default "text"), `Class` |
| **Used on** | Templates modal, ApiKeys modal |

```razor
<FloatingField Label="Title" @bind-Value="_formTitle" />
```

### 11.2 Floating Select

| | |
|---|---|
| **Component** | Inline markup using `.floating-field` + `.floating-select` CSS classes |
| **Visual** | Same floating label pattern as FloatingField, but for `<select>`. Label always floated (select always shows a value). |
| **Used on** | Templates modal (task list selector) |

```razor
<div class="floating-field">
    <select @bind="_formTaskListId" class="floating-select">
        <option value="">Select a list…</option>
        @foreach (var list in TaskLists)
        {
            <option value="@list.Id">@list.DisplayName</option>
        }
    </select>
    <label class="floating-label">Task List</label>
</div>
```

### 11.3 Toggle Switch

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | Tailwind-based toggle: `sr-only` checkbox + styled `div` with `peer-checked:after:translate-x-full peer-checked:bg-brand-600` transition. 9×5 track, 4×4 knob. |
| **Used on** | Templates modal ("Due Today" toggle) |

```razor
<label class="relative inline-flex items-center cursor-pointer">
    <input type="checkbox" class="sr-only peer" checked="@_value" @onchange="..." />
    <div class="w-9 h-5 bg-slate-200 rounded-full peer dark:bg-slate-700
                peer-checked:after:translate-x-full peer-checked:after:border-white
                after:content-[''] after:absolute after:top-[2px] after:left-[2px]
                after:bg-white after:border after:rounded-full after:h-4 after:w-4
                after:transition-all peer-checked:bg-brand-600"></div>
</label>
```

### 11.4 Standard Input

| | |
|---|---|
| **Component** | CSS class `.input` |
| **Visual** | `rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm shadow-sm focus:ring-2 focus:ring-brand-500` |
| **Used on** | ApiKeys (readonly key display) |

---

## 12. Icon Badges

### 12.1 Gradient Icon Badge (Page Header)

| | |
|---|---|
| **Component** | Inside `PageHeader.razor` |
| **Visual** | `w-10 h-10 rounded-xl bg-gradient-to-br shadow-lg` with page-specific gradient colors. Contains a 5×5 white SVG icon. |
| **Used on** | All page headers |

### 12.2 Small Icon Badge (Card Accent)

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | `w-9 h-9 rounded-xl bg-{color}-100 dark:bg-{color}-900/40 flex items-center justify-center` with 4×4 colored SVG icon. |
| **Used on** | Home template cards (brand), ApiKey cards (emerald), Home quick-create section header |

### 12.3 Emoji Icon Badge (Large)

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | `w-10 h-10 rounded-xl bg-{color}-500/20 flex items-center justify-center` with large emoji text (`text-xl`). |
| **Used on** | Landing feature cards |

---

## 13. Tab Navigation

### 13.1 Pill Tab Bar

| | |
|---|---|
| **Component** | Inline markup |
| **Visual** | `flex gap-1 bg-slate-100 dark:bg-slate-800 rounded-xl p-1 w-fit`. Active tab: `bg-white dark:bg-slate-700 shadow-sm text-slate-900 font-semibold`. Inactive: `text-slate-500 hover:text-slate-700`. |
| **Used on** | SyncSettings ("Synced" / "Not Synced") |

```razor
<div class="flex gap-1 bg-slate-100 dark:bg-slate-800 rounded-xl p-1 mb-4 w-fit">
    <button @onclick="() => _activeTab = 0"
            class="@(_activeTab == 0 ? "bg-white dark:bg-slate-700 shadow-sm text-slate-900 dark:text-white" : "text-slate-500 ...") px-4 py-2 rounded-lg text-sm font-semibold transition-all">
        Synced (@_syncedLists.Count)
    </button>
    <!-- ... -->
</div>
```

---

## 14. Decorative Elements

### 14.1 Gradient Header Bar

The app header uses `bg-gradient-to-r from-brand-700 via-brand-600 to-violet-600` as the primary brand gradient across all pages.

### 14.2 Card Top Gradient Strip

A thin gradient line at the top of cards to add visual identity:

```razor
<div class="h-1.5 bg-slate-100 dark:bg-slate-700">
    <div class="h-full bg-gradient-to-r from-violet-500 to-brand-500 ..."></div>
</div>
```

### 14.3 Landing Gradient Orbs

Blurred, pulsing gradient circles for the unauthenticated landing page:

```razor
<div class="absolute -top-40 -right-40 w-96 h-96 bg-brand-600 rounded-full
            mix-blend-multiply filter blur-3xl opacity-20 animate-pulse"></div>
```

### 14.4 Dot Grid Overlay

Subtle radial-gradient dot pattern on the landing page:

```css
background-image: radial-gradient(circle, #6366f1 1px, transparent 1px);
background-size: 32px 32px;
```

### 14.5 Status Dot

Small pulsing dot for status indicators:

```razor
<span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
```

---

## 15. Responsive Grid Layouts

| Layout | Classes | Used on |
|---|---|---|
| 3-column card grid | `grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4` | Home templates, Templates list, ApiKeys list |
| 4-column feature grid | `grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4` | Landing feature cards |
| Single-column constrained | `max-w-lg` | SyncSettings |
| Main content wrapper | `max-w-6xl mx-auto px-4 sm:px-6 py-6 sm:py-8 pb-20 lg:pb-8` | MainLayout |

---

## 16. Spinners

### 16.1 Brand Spinner (Inline)

Used inside buttons and checkboxes:

```razor
<svg class="animate-spin w-4 h-4 text-brand-600" fill="none" viewBox="0 0 24 24">
    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
</svg>
```

**Used on:** TaskStatusCheckbox, Templates save button, ApiKeys create button, SyncSettings sync indicator.

---

## 17. Section Dividers

### 17.1 Horizontal Rule

```razor
<div class="mt-8 pt-6 border-t border-slate-100 dark:border-slate-700">
    <!-- footer content -->
</div>
```

**Used on:** Home (manage templates link), sidebar user controls.

### 17.2 Group Heading

```razor
<p class="text-xs font-bold uppercase tracking-widest text-slate-400 dark:text-slate-500 mb-3">@group.Key</p>
```

**Used on:** Templates page (grouped by task list).

---

## Quick Reference: CSS Utility Classes

| Class | Definition | File |
|---|---|---|
| `.btn` | Base button styles | `tailwind-input.css` |
| `.btn-primary` | Brand-600 filled button | `tailwind-input.css` |
| `.btn-ghost` | Transparent hover button | `tailwind-input.css` |
| `.btn-danger` | Rose-600 filled button | `tailwind-input.css` |
| `.btn-outline` | Brand-600 bordered button | `tailwind-input.css` |
| `.btn-sm` / `.btn-lg` | Size modifiers | `tailwind-input.css` |
| `.input` | Standard text input | `tailwind-input.css` |
| `.card` | Card container | `tailwind-input.css` |
| `.card-hover` | Interactive card | `tailwind-input.css` |
| `.chip` | Base chip/badge | `tailwind-input.css` |
| `.chip-primary/success/error/warning/info` | Chip color variants | `tailwind-input.css` |
| `.task-row` | List item row | `tailwind-input.css` |
| `.section-title` | Sidebar section heading | `tailwind-input.css` |
| `.floating-field` | Float label wrapper | `tailwind-input.css` |
| `.floating-input` | Float label input | `tailwind-input.css` |
| `.floating-select` | Float label select | `tailwind-input.css` |
| `.floating-label` | Float label text | `tailwind-input.css` |
| `.page-container` | Page content wrapper | `tailwind-input.css` |
