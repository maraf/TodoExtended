# Proactive bUnit Testing for Shared Blazor Components

**Date:** 2026-03-11  
**Author:** Tester  
**Status:** Implemented

## Context

Frontend is currently extracting 8 shared Blazor components from duplicated page markup. To support test-driven development and ensure components meet specifications, Tester wrote comprehensive bUnit test suites for 6 core components **before the components were implemented**.

## Decision

Created a new test project `tests/TodoExtended.Components.Tests/` with bUnit + xUnit test coverage for:

1. **ModalDialog** — 7 tests covering visibility, title display, close callbacks, and body/footer RenderFragments
2. **PageHeader** — 5 tests covering title rendering, gradient classes, and icon display
3. **ErrorAlert** — 6 tests covering null/empty handling, rose styling, and warning prefix
4. **EmptyState** — 7 tests covering emoji, heading, description, and action button behavior
5. **SkeletonGrid** — 7 tests covering count, height, animate-pulse, and grid layout
6. **FloatingField** — 7 tests covering label, value binding, type attribute, and change events

## Rationale

**Proactive Testing Benefits:**
- **Contract-first design** — Tests define expected component APIs before implementation
- **Parallel development** — Frontend can implement components knowing the test requirements
- **Faster feedback** — Immediate test failures when component behavior deviates from spec
- **Documentation** — Tests serve as executable specification of component behavior
- **Regression prevention** — Components remain stable as markup is refactored

**Component Selection:**
- Focused on 6 components with clear, stable contracts (ModalDialog, PageHeader, ErrorAlert, EmptyState, SkeletonGrid, FloatingField)
- Skipped StatusBadge and additional speculative components to minimize rework risk
- All 6 components have clear parameter signatures from the spec

## Test Patterns

All tests follow established project conventions:

- **Naming:** `MethodName_Scenario_ExpectedResult` (e.g., `Render_WhenVisibleIsFalse_RendersNothing`)
- **Structure:** Arrange-Act-Assert pattern
- **Framework:** bUnit 1.32.7 + xUnit (matching .NET 10 E2E test project style)
- **Assertions:** Focus on component **contracts** (parameters → output), not exact CSS classes
- **Coverage:** Happy paths, error cases, null handling, event callbacks, conditional rendering

## Implementation Details

**Project Structure:**
```
tests/TodoExtended.Components.Tests/
├── TodoExtended.Components.Tests.csproj
├── _Imports.razor (shared using directives)
├── README.md (comprehensive test documentation)
├── ModalDialogTests.cs
├── PageHeaderTests.cs
├── ErrorAlertTests.cs
├── EmptyStateTests.cs
├── SkeletonGridTests.cs
└── FloatingFieldTests.cs
```

**Dependencies:**
- `bunit` (1.32.7) — Blazor component testing
- `xunit` (2.9.2) — Test framework
- `Microsoft.NET.Test.Sdk` (17.12.0) — Test runner

**Expected Component Location:**
- `src/TodoExtended.Web/Components/Shared/*.razor`

## Current Status

⚠️ **Tests written, components pending.** Build currently fails with expected errors:
```
error CS0234: The type or namespace name 'ModalDialog' does not exist in the namespace 'TodoExtended.Web.Components.Shared'
```

This is **expected behavior** — tests are ready for Frontend to implement components.

## Integration Notes

When Frontend creates the actual components:

1. Tests may need **minor adjustments** for:
   - Exact parameter names (if different from spec)
   - CSS selector patterns (if markup structure varies)
   - Event callback signatures (if extended)

2. Run tests to validate implementation:
   ```bash
   cd tests/TodoExtended.Components.Tests
   dotnet build
   dotnet test
   ```

3. All 39 tests should pass once components match specifications

## Impact

- **Test coverage:** 39 test cases across 6 components
- **Zero breaking changes** — New test project, no modifications to existing code
- **Documentation:** README.md provides component specifications and expected parameters
- **Team coordination:** Tests define the contract between Tester and Frontend agents
