# Squad Decisions

## Active Decisions

### Build Prevention: Exclude Project-Local Artifacts from SDK Default Items

**Date:** 2026-04-13  
**Author:** Backend  
**Status:** Implemented

**Problem:** `dotnet run --project src\TodoExtended.Web\` failed during compilation with duplicate generated-source errors, including duplicate assembly attributes and `ValidatableTypeAttribute`.

**Root Cause:** `src\TodoExtended.Web\artifacts\copilot-stt\obj\Debug\net10.0\` contained generated C# files that duplicated the project's normal `obj\Debug\net10.0\` outputs. Because `artifacts\` lives under the project directory and was not excluded from SDK default items, stale generated files were added to `Compile` and built alongside the real generated files.

**Decision:** Exclude the project-local `artifacts\` tree from default SDK item discovery via `DefaultItemExcludes` in `TodoExtended.Web.csproj`.

**Implementation:**
```xml
<DefaultItemExcludes>$(DefaultItemExcludes);artifacts\**</DefaultItemExcludes>
```
*Already in place at `src/TodoExtended.Web/TodoExtended.Web.csproj:9`*

**Validation:**
- ✅ `dotnet msbuild .\src\TodoExtended.Web\TodoExtended.Web.csproj -nologo -getItem:Compile` no longer includes `artifacts\...` files
- ✅ `dotnet build .\src\TodoExtended.Web\TodoExtended.Web.csproj -nologo` succeeds
- ✅ Clean builds resolve duplicate attribute errors

**Related Decision:** "Build: Duplicate Assembly Attribute & ValidatableTypeAttribute Errors" — documents the incident and operator cleanup actions that revealed this safeguard need.

---

### Garmin Tag Task Title Trimming (Final)

**Date:** 2026-04-09  
**Author:** Frontend  
**Status:** Implemented

In the Garmin tag tasks view, task titles now drop a leading selected tag when it appears at the start of the title, matching case-insensitively.

**Key Decisions:**
1. **Only strip `#tag` prefix** — Case-insensitive match for the hashtag form of the tag name
2. **Keep original if trimming would blank it** — Avoids empty rows on the watch for titles that are only the tag
3. **Limit to tag tasks view** — Other task rendering remains unchanged

### ChatInput.razor — Browser-Resolvable Module Specifier for Dynamic Import

**Date:** 2026-04-13  
**Author:** Frontend / Tester / Hockney validation  
**Status:** Implemented

Dynamic import of fingerprinted `.razor.js` assets from Blazor components requires a browser-resolvable module specifier. The static asset path `Assets["Components/Shared/ChatInput.razor.js"]` returns `Components/Shared/ChatInput.{fingerprint}.razor.js`, which the browser rejects as a bare module specifier during `IJSRuntime.InvokeAsync("import", ...)`.

**Decision:** Normalize asset paths to include a leading `./` prefix, converting bare paths into relative URL specifiers that the browser can resolve:

```csharp
var assetPath = Assets["Components/Shared/ChatInput.razor.js"];
var moduleUrl = assetPath.StartsWith("./", StringComparison.Ordinal) || assetPath.StartsWith("/", StringComparison.Ordinal)
    ? assetPath
    : $"./{assetPath}";
_jsModule = await JS.InvokeAsync<IJSObjectReference>("import", moduleUrl);
```

**Rationale:**
- Fixes runtime failure: `Failed to resolve module specifier 'Components/Shared/ChatInput...razor.js'`
- Preserves static asset fingerprinting pattern (works in `<script src>` attributes)
- Aligns with browser URL resolution rules without breaking existing patterns
- Maintains compatibility with debug logging for troubleshooting JS module load issues

**Validation:**
- ✅ Component renders textarea/button when JS init fails (fallback)
- ✅ Component renders mic button when JS loads and speech support detected
- ✅ `dotnet build src\TodoExtended.Web\TodoExtended.Web.csproj` passes clean
- ✅ `dotnet test tests\TodoExtended.Components.Tests\TodoExtended.Components.Tests.csproj --filter ChatInput` passes

**Files Changed:**
- `src/TodoExtended.Web/Components/Shared/ChatInput.razor` — Module URL normalization
- `tests/TodoExtended.Components.Tests/ChatInputTests.cs` — Regression coverage for module specifier

**Key Learnings:**
- `IJSRuntime.InvokeAsync("import", ...)` must receive a browser-resolvable specifier; bare `Components/...` paths fail
- bUnit `SetupModule(...)` pattern effective for testing dynamic import behavior
- Relative path `./{assetPath}` variant preferred over absolute URL approach for static asset simplicity

### Build: Duplicate Assembly Attribute & ValidatableTypeAttribute Errors (Resolved)

**Date:** 2026-04-13  
**Author:** Hockney (Runner/DevOps) / Backend (Architecture Investigation)  
**Status:** Resolved

**Problem:** `dotnet build` and `dotnet run --project src\TodoExtended.Web\` failed with:
- `error CS0579: Duplicate 'global::System.Runtime.Versioning.TargetFrameworkAttribute'`
- `error CS0579: Duplicate 'System.Reflection.AssemblyCompanyAttribute'`
- `error CS0101: The namespace 'Microsoft.Extensions.Validation.Embedded' already contains a definition for 'ValidatableTypeAttribute'`

**Root Cause:** Stale `.NET SDK` auto-generated assembly info files in `src\TodoExtended.Web\artifacts\copilot-stt\obj\Debug\net10.0\` conflicted with fresh copies in `obj\Debug\net10.0\`. The `artifacts/` directory is gitignored but contained stale build outputs that MSBuild processed, causing duplicate definitions.

**Decision:** Remove orphaned artifact directories and perform a clean rebuild when duplicate attribute errors occur.

**Actions Taken:**
1. Removed `src\TodoExtended.Web\artifacts\copilot-stt\` directory (orphaned temporary build outputs)
2. Removed `src\TodoExtended.Web\obj\` and `src\TodoExtended.Web\bin\` directories
3. Killed `VBCSCompiler.exe` and `TodoExtended.Web.exe` processes (file locks)
4. Ran clean `dotnet build src\TodoExtended.Web\` verification

**Validation:**
- ✅ `dotnet build src\TodoExtended.Web\TodoExtended.Web.csproj -nologo` → Success (0 errors, 0 warnings)
- ✅ No duplicate attribute errors
- ✅ Clean rebuild from scratch confirmed working

**Prevention for future CI/CD:**
- Ensure `artifacts/` is truly removed during clean builds (not just `obj/bin/`)
- Consider using `git clean -fdx` in CI pipelines
- Warn developers if stale `artifacts/` directories persist locally

**Key Learning:** Gitignore is not cleanup. A directory in `.gitignore` is invisible to git but can still contain stale build outputs that interfere with compilation. SDK auto-generation conflicts occur when MSBuild finds duplicate generated files in multiple locations (e.g., `obj/` and sibling `artifacts/obj/`).

---

### ChatInput.razor — Active Speech-to-Text State Visibility

**Date:** 2026-04-13  
**Author:** Frontend / Tester  
**Status:** Implemented

When speech-to-text is active in `ChatInput`, keep the microphone icon visible and indicate the active state with brand-colored icon + outline styling instead of replacing the icon with a recording dot.

**Decision:**
1. **Icon remains visible** — No replacement with recording dot or other indicator
2. **Visual active state** — Brand-colored icon + outline/border on the button
3. **Semantic accessibility** — Full `aria-pressed` state + context-aware `aria-label` (`Start recording` / `Stop recording`)
4. **Test contract** — bUnit coverage via DOM semantic assertions, not screenshot-only

**Rationale:**
- Matches the approved visual target in `Screenshot 2026-04-13 103959.png`
- Preserves the send button layout and the mic button footprint
- Makes the toggle state clearer without introducing warning/error-red semantics
- Enables reliable testing via semantic markers (aria-pressed, aria-label, active classes)

**Files Changed:**
- `src/TodoExtended.Web/Components/Shared/ChatInput.razor` — Icon state styling + accessibility
- `tests/TodoExtended.Components.Tests/ChatInputTests.cs` — Active state + reset-to-idle coverage

**Key Learnings:**
- Focused bUnit coverage is practical for component state testing without full browser rendering
- Stable assertions: verify `aria-label`, `aria-pressed`, active CSS classes, and rendered icon rather than screenshot-perfect visuals
- Test coverage validates state transitions (idle → recording → idle) independently of JS animation

---

### ChatMessageBubble.razor — Render AI Chat Messages as Markdown-Formatted HTML

**Date:** 2026-04-20  
**Authors:** Frontend / Tester / Hockney (Validation)  
**Status:** Implemented

Render assistant chat messages as sanitized markdown-derived HTML instead of plain text. User chat messages remain plain text to preserve exact input.

**Problem:** OpenAI responses include markdown formatting (bold, italic, lists, task checkboxes). Rendering them as plain text exposes raw `**`, `_`, and `- [ ]` markers, degrading readability.

**Decision:**
1. **Assistant messages use Markdig markdown rendering** — Convert OpenAI text to HTML with `.DisableHtml()` for XSS protection
2. **User messages remain plain text** — Preserve exact user input without auto-formatting
3. **Task-list references injected as links** — Convert task list display names to `/tasks/{id}` links before markdown rendering
4. **Markdown features enabled:** bold, italic, lists, checkboxes (read-only)

**Implementation:**

```csharp
private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .DisableHtml()
    .Build();
```

**Supported Markdown:**
- Bold: `**text**` → `<strong>text</strong>`
- Italic: `_text_` → `<em>text</em>`
- Lists: `- item` → `<ul><li>item</li></ul>`
- Checkboxes: `- [ ] item` → `<input type="checkbox" disabled>` (read-only)

**Security Properties:**
- ✅ **XSS Protection:** Raw HTML tags stripped via `.DisableHtml()`
- ✅ **Injection Prevention:** Task list names URL-escaped in link hrefs
- ✅ **Script Blocking:** No `<script>` execution possible via markdown
- ✅ **External Resources:** No image/iframe loading by design

**Test Coverage:**
- `Render_AssistantMarkdown_RendersFormattedHtmlInsteadOfLiteralMarkers` — Validates markdown formatting and HTML safety
- `Render_AssistantMarkdownWithTaskListReference_RendersLinkedFormattedContent` — Validates task list link injection
- Full component test suite: 65/65 passing

**Files Changed:**
- `src/TodoExtended.Web/Components/Shared/ChatMessageBubble.razor` — Markdown-based HTML rendering for assistant messages
- `tests/TodoExtended.Components.Tests/ChatMessageBubbleTests.cs` — Test coverage for markdown + task list linking

**Validation:**
- ✅ `dotnet build src\TodoExtended.Web\TodoExtended.Web.csproj` → Success (0 errors, 0 warnings)
- ✅ ChatMessageBubble tests: 2/2 passed
- ✅ Full component suite: 65/65 passed
- ✅ Commit df020c1 pushed to `copilot/extend-web-configuration-support`

**Key Learnings:**
- Markdown rendering is the correct approach for OpenAI responses because they naturally include markdown formatting
- Markdig is a battle-tested markdown library with built-in XSS protection
- `.DisableHtml()` prevents HTML injection without breaking markdown features
- Markdown pipeline is static (instantiated once), no per-message overhead
- Task list link injection happens before markdown rendering (simpler logic)

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
