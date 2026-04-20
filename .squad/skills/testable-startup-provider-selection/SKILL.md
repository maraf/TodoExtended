---
name: "testable-startup-provider-selection"
description: "Extract provider-routing logic from Program.cs into a testable helper"
domain: "backend"
confidence: "high"
source: "earned"
tools:
  - name: "dotnet test"
    description: "Runs focused regression coverage for provider selection"
    when: "After moving startup routing or configuration checks into a helper"
---

## Context
Use this when `Program.cs` or other startup wiring contains branching logic that decides which implementation/provider to register at runtime. The goal is to keep startup thin and make provider fallback rules unit-testable without booting the whole app.

## Patterns
- Move provider-selection rules into a small helper in the same feature namespace.
- Keep startup responsible for DI wiring only; let the helper answer “is this provider configured?” and “which provider should this user get?”
- Use `string.IsNullOrWhiteSpace` for configuration gates so blank secrets or deployment names do not count as enabled.
- If multiple providers exist, encode the default/fallback order explicitly in the helper and cover it with unit tests.
- When user routing depends on claims, centralize claim lookup in the helper so docs and tests stay aligned.

## Examples
- `src\TodoExtended.Web\Services\AiChat\AiChatProviderSelection.cs`
- `src\TodoExtended.Web\Program.cs`
- `tests\TodoExtended.Tests\Services\AiChat\AiChatProviderSelectionTests.cs`

## Anti-Patterns
- Embedding multi-branch provider fallback logic inline in `Program.cs`
- Treating whitespace config values as valid enablement
- Having runtime-only behavior with no direct unit-test coverage
