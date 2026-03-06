# Decision: Flowbite Blazor UI Redesign

**Date:** 2025-07-22  
**Author:** Frontend  
**Status:** Implemented

## Context

Migrated all UI from Bootstrap to Flowbite Blazor components + Tailwind CSS utility classes.

## Key Decisions

1. **Native HTML inputs over Flowbite form components** — Used native `<input>`, `<select>`, `<checkbox>` styled with Tailwind classes (matching Flowbite's visual design) instead of `<TextInput>`, `<Select>` components. Reason: more reliable `@bind` behavior in Blazor.

2. **`@using static` for nested enums** — Added `@using static Flowbite.Components.Badge` and `@using static Flowbite.Components.Button` to `_Imports.razor` to bring nested enum types (`ButtonColor`, `BadgeColor`, etc.) into scope.

3. **Card-styled divs for task lists** — Used raw Tailwind card styling for list containers (`bg-white rounded-lg border shadow-sm divide-y`) rather than the `<Card>` component, giving finer control over padding and item separation.

4. **All dark mode compatible** — Every custom Tailwind class includes `dark:` variants.

## Impact

- All 8 UI component files redesigned
- `_Imports.razor` updated with 4 new Flowbite namespace imports
- Zero Bootstrap classes remain in the codebase
- Build: zero errors, zero warnings
