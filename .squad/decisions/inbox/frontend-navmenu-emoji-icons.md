# Decision: NavMenu Emoji Icon Rendering

**Date:** 2026-03-07  
**Author:** Frontend  
**Status:** Implemented

## Context

Task list names in Microsoft To Do can contain a leading Unicode emoji (e.g., "🐶Domeczech"). The nav menu should extract this emoji and display it as a visual icon prefix, stripping it from the text to avoid duplication.

## Decision

- **Emoji extraction** uses `StringInfo.GetTextElementEnumerator()` for grapheme-cluster-safe parsing, with `Rune`-based Unicode range checks to identify emoji characters. This correctly handles multi-byte, surrogate pair, and ZWJ emoji sequences.
- **Rendering approach:** Since MudBlazor's `MudNavLink.Icon` only accepts SVG path strings, the emoji is rendered as a styled `<span class="nav-emoji-icon">` inside the nav link's child content, with CSS isolation matching the Material icon slot dimensions (24×24px, 1.25rem font).
- **URL preservation:** The original `DisplayName` (with emoji) is kept in the Href query string for downstream use.
- **Graceful fallback:** If no leading emoji is detected, the nav link renders plain text with no icon prefix, matching previous behavior.

## Impact

- Files: `NavMenu.razor`, `NavMenu.razor.css` (new)
- No breaking changes; lists without emoji display identically to before
