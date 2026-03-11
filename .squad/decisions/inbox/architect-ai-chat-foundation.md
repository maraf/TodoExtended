# Decision: AI Chat Foundation Architecture

**Date:** 2026-03-11
**Author:** Architect
**Issue:** #22
**Status:** Active

## Context

Adding an AI chat feature that lets users manage tasks via natural language. The AI can read task lists/tasks and propose write actions (create, complete, uncomplete) that require user confirmation before execution.

## Decisions

### 1. SDK: Microsoft.Extensions.AI (provider-agnostic)
- Uses `IChatClient` abstraction, not OpenAI SDK directly.
- Allows swapping providers (GitHub Models, Azure OpenAI, local models) without code changes.
- Packages: `Microsoft.Extensions.AI` 10.4.0 + `Microsoft.Extensions.AI.OpenAI` 10.4.0.

### 2. Pattern: Structured tool-calling, NOT free-text parsing
- AI returns `ToolCall` objects with typed parameters.
- We map tool calls → `ProposedAction[]` for user confirmation.
- No regex/string parsing of natural language — the AI SDK handles structured extraction.

### 3. Confirm-before-execute for write operations
- Read tools (get_task_lists, get_tasks, get_today_tasks) execute immediately to feed AI context.
- Write tools (create_task, complete_task, uncomplete_task) produce `ProposedAction` cards.
- User must explicitly approve each action before execution.

### 4. StubChatService as default/demo fallback
- `StubChatService` returns a "not configured" message.
- In demo mode, this is the permanent implementation.
- In production, Backend agent will add conditional registration when `AiChat:ApiKey` is set.

### 5. Config via AiChatOptions bound to "AiChat" section
- Endpoint, model, API key, max history messages.
- API key stored in user-secrets or environment variables (never in appsettings.json).
