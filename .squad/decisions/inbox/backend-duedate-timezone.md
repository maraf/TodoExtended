# Decision: Use DateOnly for To Do due dates

**Author:** Backend  
**Date:** 2025-07-15  
**Status:** Proposed

## Context

The `dueDateTime` field from Microsoft Graph's To Do API is a `dateTimeTimeZone` with separate `dateTime` and `timeZone` fields. The previous code used `DateTimeOffset.Parse(dateTimeString)` which ignored the `timeZone` field and applied the server's local timezone offset, causing dates to shift by +/- 1 day depending on the server/user timezone (CET user seeing "tomorrow" instead of "today").

## Decision

- **DTOs use `DateOnly?`** instead of `DateTimeOffset?` for `DueDate` (renamed from `DueDateTime`).
- **Parsing uses `DateTimeStyles.RoundtripKind`** via a `ParseDueDate` helper to prevent `DateTime.Parse` from converting UTC timestamps to local time.
- **OData filter uses `DateTime.UtcNow`** for "today" comparison since Graph stores due dates at UTC midnight.

## Rationale

Due dates in Microsoft To Do are conceptually date-only (no meaningful time component). Using `DateOnly` eliminates all timezone-related date shifts and makes the API semantically correct. The `timeZone` field in `dateTimeTimeZone` is irrelevant for date-only values when we extract just the date portion.

## Impact

- `TodoTask.DueDateTime` → `TodoTask.DueDate` (breaking change for any consumers)
- `TodoTaskWithList.DueDateTime` → `TodoTaskWithList.DueDate`
- `Tasks.razor` updated to use `DueDate`
