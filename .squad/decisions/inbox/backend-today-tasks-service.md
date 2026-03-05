# Decision: TodoTaskWithList record for cross-list task views

**Author:** Backend  
**Date:** 2025-07-14  
**Status:** Proposed

## Context

The "Today" view needs to show tasks from all lists in a flat list. Tasks from `GetTasksAsync` don't carry their parent list name, so the UI can't show which list a task belongs to.

## Decision

Introduced a `TodoTaskWithList` record that mirrors `TodoTask` but adds `ListId` and `ListName` fields. `GetTodayTasksAsync()` returns `IReadOnlyList<TodoTaskWithList>`.

## Rationale

- Keeps `TodoTask` unchanged (no breaking changes to existing consumers).
- The extra record is a simple extension, not inheritance, following the existing record-based DTO pattern.
- `ListId` included alongside `ListName` so the frontend can link back to the full list if needed.

## Alternatives Considered

- Adding optional `ListName?` to `TodoTask` — rejected because it would be null in most call sites and muddies the contract.
- Returning a grouped dictionary — rejected because the UI wants a flat "today" list.
