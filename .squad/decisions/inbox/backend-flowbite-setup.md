# Decision: Flowbite Blazor Infrastructure Setup

**Author:** Backend  
**Date:** 2025-07-17  
**Status:** Implemented

## Context

Migrating from Bootstrap to Flowbite Blazor component library with Tailwind CSS for the UI layer.

## Decisions

1. **Flowbite.Components.Activity vs System.Diagnostics.Activity ambiguity:** Resolved by fully qualifying `System.Diagnostics.Activity` in Error.razor rather than removing the `@using Flowbite.Components` global import. The global import benefits all other pages.

2. **Tailwind CSS v4 via CDN:** Using `https://cdn.jsdelivr.net/npm/@@tailwindcss/browser@@4` (browser build) for development. This should be replaced with a proper build pipeline for production.

3. **Bootstrap removal is breaking for existing UI:** All Bootstrap CSS classes in layout and page components will stop rendering correctly. The Frontend agent's layout rewrite must land alongside or after this change.

## Impact

- Frontend team: All components must now use Tailwind utility classes + Flowbite components instead of Bootstrap
- Scoped CSS files for MainLayout and NavMenu have been deleted — layout must use inline Tailwind classes
