# TodoExtended.Components.Tests

📌 **Proactive Test Suite** — Tests for shared Blazor components being extracted by Frontend.

## Test Coverage

This test project contains bUnit tests for 6 shared Blazor components that are being extracted from page markup:

### 1. ModalDialog.razor
**Location:** `src/TodoExtended.Web/Components/Shared/ModalDialog.razor`

**Expected Parameters:**
- `bool Visible` — Controls modal visibility
- `string Title` — Modal title text
- `EventCallback OnClose` — Close button click handler
- `RenderFragment Body` — Modal body content
- `RenderFragment? Footer` — Optional footer content

**Tests:**
- ✅ Renders nothing when Visible=false
- ✅ Renders overlay + dialog when Visible=true
- ✅ Displays title in header
- ✅ Close button triggers OnClose callback
- ✅ Renders Body RenderFragment
- ✅ Renders Footer RenderFragment when provided
- ✅ Handles null Footer gracefully

### 2. PageHeader.razor
**Location:** `src/TodoExtended.Web/Components/Shared/PageHeader.razor`

**Expected Parameters:**
- `string Title` — Page title
- `RenderFragment? Icon` — Optional icon content (SVG/emoji)

**Tests:**
- ✅ Displays title in h1 element
- ✅ Applies gradient background classes
- ✅ Renders icon content when provided
- ✅ Renders title when Icon is null
- ✅ Icon container has correct dimensions (w-10 h-10)

### 3. ErrorAlert.razor
**Location:** `src/TodoExtended.Web/Components/Shared/ErrorAlert.razor`

**Expected Parameters:**
- `string? Message` — Error message (null/empty = no render)

**Tests:**
- ✅ Renders nothing when Message is null
- ✅ Renders nothing when Message is empty
- ✅ Renders nothing when Message is whitespace
- ✅ Shows rose-colored alert with message
- ✅ Contains ⚠ prefix
- ✅ Applies correct rose styling (bg, border, text)

### 4. EmptyState.razor
**Location:** `src/TodoExtended.Web/Components/Shared/EmptyState.razor`

**Expected Parameters:**
- `string Emoji` — Emoji icon
- `string Heading` — Main heading text
- `string Description` — Description text
- `string? ActionLabel` — Optional action button label
- `EventCallback OnAction` — Action button click handler

**Tests:**
- ✅ Displays emoji
- ✅ Displays heading in h3
- ✅ Displays description
- ✅ Shows action button when ActionLabel provided
- ✅ Hides button when ActionLabel is null
- ✅ Action button click triggers OnAction callback
- ✅ Applies centered styling (items-center, justify-center, text-center)

### 5. SkeletonGrid.razor
**Location:** `src/TodoExtended.Web/Components/Shared/SkeletonGrid.razor`

**Expected Parameters:**
- `int Count = 3` — Number of skeleton items
- `string Height = "h-32"` — Tailwind height class

**Tests:**
- ✅ Renders 3 skeleton items by default
- ✅ Renders correct number with custom Count
- ✅ Applies animate-pulse class
- ✅ Applies default h-32 height
- ✅ Applies custom height class
- ✅ Renders nothing when Count=0
- ✅ Uses grid layout

### 6. FloatingField.razor
**Location:** `src/TodoExtended.Web/Components/Shared/FloatingField.razor`

**Expected Parameters:**
- `string Label` — Field label
- `string Value` — Input value
- `EventCallback<string> ValueChanged` — Value change handler
- `string Type = "text"` — Input type

**Tests:**
- ✅ Displays label
- ✅ Displays input with bound value
- ✅ Applies type attribute
- ✅ Uses "text" as default type
- ✅ Input change propagates ValueChanged event
- ✅ Uses floating label pattern (CSS classes)
- ✅ Input has placeholder attribute

## Current Status

⚠️ **Tests are written but components don't exist yet.** This is expected — Frontend is creating the components in parallel.

Build error example:
```
error CS0234: The type or namespace name 'ModalDialog' does not exist in the namespace 'TodoExtended.Web.Components.Shared'
```

## Running Tests

Once Frontend creates the components:

```bash
cd tests/TodoExtended.Components.Tests
dotnet build
dotnet test
```

## Notes

- Tests focus on **component contracts** (parameters → rendered output), not exact CSS classes
- Tests verify:
  - Parameter binding works correctly
  - EventCallbacks are invoked
  - Conditional rendering based on parameters
  - RenderFragments are rendered
  - Default parameter values
- Tests use bUnit's `RenderComponent<T>()` and `Find()` / `FindAll()` assertions
- xUnit assertions: `Assert.Contains()`, `Assert.Empty()`, `Assert.True()`, etc.

## Integration Notes

When Frontend finalizes component APIs, tests may need minor adjustments for:
- Exact parameter names (if different from spec)
- CSS selector patterns (if markup structure varies)
- Event callback signatures (if extended beyond spec)

All tests follow the **Arrange-Act-Assert** pattern and use descriptive naming: `MethodName_Scenario_ExpectedResult`
