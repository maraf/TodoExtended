# Use TodoExtended_icon.svg as App Brand Icon

**Date:** 2026-03-11
**Author:** Frontend
**Status:** Implemented

## Decision

Replaced all app branding visuals with the new golden yellow `TodoExtended_icon.svg`:

1. **Favicon** (`App.razor`): Changed from `favicon.png` (PNG) to `TodoExtended_icon.svg` (SVG) with `type="image/svg+xml"`
2. **App bar logo** (`MainLayout.razor`): Replaced `✓` text-in-div placeholder with `<img>` rendering the SVG icon
3. **Landing page logo** (`Home.razor`): Replaced gradient-background `✓` div with `<img>` rendering the SVG icon

## Rationale

- The golden icon visually distinguishes TodoExtended from the original Microsoft To Do (blue icon)
- SVG favicon is resolution-independent and renders crisply on all displays
- Using the actual icon file instead of text placeholders gives the app a polished, branded appearance

## Impact

- Files changed: `App.razor`, `MainLayout.razor`, `Home.razor`
- `Microsoft_To-Do_icon.svg` remains in wwwroot but is no longer referenced (can be removed if desired)
- `favicon.png` is no longer referenced (can be removed if desired)
- No breaking changes
