# API Keys Page Card-Based Redesign

**Author:** Frontend  
**Date:** 2026-03-06  
**Status:** Implemented

## Decision

Redesigned ApiKeys.razor from MudDataGrid + always-visible form to card-based layout with MudDialog creation, matching the Templates page pattern.

## Key Changes

1. **Card-based display** — Each API key rendered as a MudCard in a responsive MudGrid (3 cols desktop, 1 mobile). Cards show key name prominently with avatar icon, created date, and last-used date as secondary info with icons.
2. **MudDialog for creation** — Replaced the always-visible MudPaper form with a "New API Key" button that opens a MudDialog. Cleaner UX, consistent with Templates.
3. **Empty state** — Dashed-border pattern with VpnKey icon + CTA button, matching Templates page style.
4. **Newly created key alert** — Moved outside the loading/error conditional so it's always visible after creation. Uses `Variant.Filled` for stronger visual prominence.
5. **Card actions via MudMenu** — Revoke action accessible through three-dot menu on each card, consistent with Templates' edit/delete pattern.
6. **Snackbar feedback** — Added snackbar confirmation on successful key creation (was missing before).

## MainLayout Fix

Added `margin-top: var(--mud-appbar-height)` to MudMainContent to prevent page headings from being hidden behind the sticky MudAppBar.
