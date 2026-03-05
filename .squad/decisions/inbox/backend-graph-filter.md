# Decision: Use Server-Side OData Filtering for Graph To Do Tasks

**Date:** 2025-07-15
**Author:** Backend
**Status:** Implemented

## Context

`GetTodayTasksAsync()` was fetching ALL tasks from ALL lists and filtering client-side by due date. This pulls unnecessary data over the wire and won't scale as users accumulate tasks.

## Decision

Use the Graph API's `$filter` OData query parameter to filter tasks server-side by `dueDateTime/dateTime` using a date range (`ge` start of day, `lt` start of next day).

Filter string: `dueDateTime/dateTime ge '{today}T00:00:00' and dueDateTime/dateTime lt '{tomorrow}T00:00:00'`

## Rationale

- The Microsoft Graph To Do API supports `$filter` on `dueDateTime/dateTime` with relational operators.
- Server-side filtering reduces payload size and network latency.
- The SDK's fluent API (`config.QueryParameters.Filter`) cleanly supports this pattern.

## Known Limitations

- Complex `$filter` expressions combining `and`/`or` with parentheses may be unreliable on the To Do API.
- If server-side filtering ever fails at runtime, a fallback to client-side filtering could be added.
