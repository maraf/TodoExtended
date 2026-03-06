# TaskTemplate Id: Autoincrement Int → Guid

**Date:** 2026-03-06
**Author:** Backend
**Status:** Implemented

## Decision

Replaced `TaskTemplate.Id` from autoincrement `int` to `Guid` (generated client-side via `Guid.NewGuid()`). The API, service layer, and UI all use Guid identifiers.

## Rationale

- Sequential integer IDs leak information (row count, insertion order) and are predictable
- GUIDs are safe to expose publicly and don't reveal database internals
- Aligns with the team preference to not expose autoincrement IDs in the API

## Migration Strategy

SQLite doesn't support `ALTER COLUMN`, so the EF Core migration uses a table-rebuild:
1. Create new table with TEXT primary key
2. Copy existing rows with SQLite-generated UUID v4 values (`randomblob`)
3. Drop old table, rename new one

**Impact:** Existing template IDs change. Since templates are local-only data and not referenced externally, this is safe.
