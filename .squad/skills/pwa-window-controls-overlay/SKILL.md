# Skill: PWA Window Controls Overlay Integration

**Domain:** Progressive Web Apps, CSS, Frontend  
**Complexity:** Intermediate  
**Last Updated:** 2026-03-XX

## Overview

Implement Window Controls Overlay (WCO) to extend a web app's content into the native title bar area of an installed PWA, creating a more seamless desktop experience.

## When to Use

- Building a desktop-focused PWA with prominent branding in the header
- Want to eliminate visual discontinuity between app and title bar
- Need to maximize vertical screen real estate
- Targeting Chromium-based browsers (Edge, Chrome, Opera)

## Prerequisites

- Valid PWA manifest.json
- Fixed or sticky header element in app layout
- Understanding of CSS custom properties (env() function)

## Implementation Steps

### 1. Opt-in via Manifest

Add `display_override` with WCO as first choice:

```json
{
  "display": "standalone",
  "display_override": ["window-controls-overlay"],
  "theme_color": "#yourcolor"
}
```

### 2. Add Theme Color Meta Tag

Ensure consistent theming:

```html
<meta name="theme-color" content="#yourcolor" />
```

### 3. Mark Header with CSS Classes

Add semantic classes for WCO regions:

```html
<header class="wco-header">
  <div class="wco-no-drag">
    <button>Interactive</button>
    <a href="/">Link</a>
  </div>
</header>
```

### 4. Add WCO CSS Rules

Create media query scoping WCO-specific styles:

```css
@media (display-mode: window-controls-overlay) {
  .wco-header {
    /* Make header draggable */
    -webkit-app-region: drag;
    app-region: drag;
    
    /* Push content below title bar controls */
    padding-top: env(titlebar-area-y, 0);
  }
  
  /* Make interactive elements clickable */
  .wco-no-drag,
  .wco-no-drag * {
    -webkit-app-region: no-drag;
    app-region: no-drag;
  }
}
```

## Key Principles

1. **Progressive Enhancement:** WCO styles only apply when media query matches; browser/mobile unchanged
2. **Draggable by Default:** Header should be draggable for window repositioning
3. **Explicit No-Drag:** Every interactive element must be marked non-draggable
4. **CSS Environment Variables:** Use `env(titlebar-area-*)` for dynamic layout adjustments
5. **Vendor Prefixes:** Include both `-webkit-` and standard properties for compatibility

## Common Patterns

### Pattern 1: Fixed Header with Gradient Background

```css
@media (display-mode: window-controls-overlay) {
  .wco-header {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    -webkit-app-region: drag;
    app-region: drag;
    padding-top: env(titlebar-area-y, 0);
    background: linear-gradient(to right, #start, #middle, #end);
  }
}
```

### Pattern 2: Split Header (Sidebar + Main Content)

```html
<header class="wco-header">
  <div class="wco-no-drag sidebar-section">
    <img src="logo.svg" alt="Logo" />
    <span>App Name</span>
  </div>
  <div class="wco-no-drag main-section">
    <button>Menu</button>
    <div class="page-title">Current Page</div>
    <button>Settings</button>
  </div>
</header>
```

## Gotchas & Edge Cases

1. **Child Element Inheritance:** `app-region: drag` inherits to children. Must explicitly set `no-drag` on interactive elements AND their children (`wco-no-drag *`)

2. **Title Bar Height Variability:** Height varies by OS, display scaling, and browser. Always use `env(titlebar-area-y)` dynamically, never hardcode

3. **Browser Support Detection:** No JavaScript API to detect support; rely on media query and graceful fallback

4. **Mobile Impact:** WCO has zero effect on mobile; media query won't match in mobile browser or standalone mode

5. **Testing:** Must test in actual installed PWA, not browser DevTools PWA simulation

## Testing Checklist

- [ ] Install PWA and verify gradient extends into title bar
- [ ] Test window dragging by dragging header
- [ ] Verify all buttons/links in header are clickable
- [ ] Check fallback in non-WCO browsers (standalone mode)
- [ ] Test on different display scales (100%, 125%, 150%)
- [ ] Verify mobile/browser modes unchanged

## Browser Support

- ✅ Chrome/Edge 105+ (Windows, macOS, Linux)
- ✅ Opera (Chromium-based)
- ❌ Firefox (not supported as of 2024)
- ❌ Safari (not supported as of 2024)
- Fallback: Displays as normal standalone PWA

## References

- [Window Controls Overlay API - MDN](https://developer.mozilla.org/en-US/docs/Web/API/Window_Controls_Overlay_API)
- [CSS env() - MDN](https://developer.mozilla.org/en-US/docs/Web/CSS/env)
- [display_override - MDN](https://developer.mozilla.org/en-US/docs/Web/Manifest/display_override)

## Related Skills

- Progressive Web Apps (PWA)
- CSS Custom Properties
- Responsive Design
- Tailwind CSS Integration
