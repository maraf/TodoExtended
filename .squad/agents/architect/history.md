# Architect History

<!-- Session logs appended by Scribe -->

## Learnings

### 2025-07-19 — Task Templates Design
- Designed the Task Templates feature: local SQLite storage via EF Core, `ITemplateService` for CRUD + execution, `CreateTaskAsync` added to `ITodoService`.
- Key pattern: templates store `TaskListId` + cached `TaskListName` to avoid Graph calls for display. Accepted staleness tradeoff.
- Home page gets quick-create buttons; new `/templates` page for CRUD management.
- Project uses .NET 10, Blazor InteractiveServer, primary constructors, file-scoped namespaces, `[PersistentState]`, and consistent MSAL exception handling across pages.
- No user ID needed — single-user local app. Noted as a future concern if multi-user is ever needed.
- Auto-migrate at startup is appropriate for a local-only SQLite app.
