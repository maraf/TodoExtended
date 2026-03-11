# Decision: Shared Component Library Pattern

**Date:** 2026-03-11
**Author:** Frontend
**Status:** Implemented

## Context

8 duplicated markup patterns existed across 6 pages. User directive: "No copy- if markup appears in more than one place, extract it."paste 

## Decision

Created 6 shared components in `Components/Shared/`:
- ** overlay+backdrop+header/body/footer structureModalDialog** 
- ** SectionContent with gradient icon badgePageHeader** 
- ** conditional rose-colored error bannerErrorAlert** 
- ** card with icon/heading/description/optional actionEmptyState** 
- ** loading placeholder gridSkeletonGrid** 
- ** floating-label text input with two-way bindingFloatingField** 

Skipped TaskItemRow (#6) and StatusBadge (# insufficient duplication or too divergent to justify extraction.8) 

## Tailwind JIT Rule

All Tailwind class parameters must be passed as **complete class names** (e.g. `from-amber-400`, not `amber-400`) so the JIT scanner finds them in calling pages.

## Impact

~200 lines of duplicated markup eliminated. New pages should use these components instead of copying markup.
