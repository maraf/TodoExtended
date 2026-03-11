# Orchestration: Code  Delta Query CachingReview 

**Date:** 2026-03-05  
**Agent:** Code Review  
**Status:** Complete

## Task

Review Backend implementation of CachedTodoService + delta caching entities. Check correctness, performance, consistency.

## Findings

**Two critical issues detected:**

1. **SemaphoreSlim Instance Reuse (Race Condition)**
   - Backend stored `SemaphoreSlim` as static instance
   - Multiple concurrent requests share same lock state
   - **Risk:** Sync operation blocks unrelated requests; potential deadlock
   - **Fix:** Make instance-scoped (stored in CachedTodoService instance)

2. **Soft-Delete Resurrection (Query Flaw)**
   - Some query paths forgot `&& !IsDeleted` filter
   - **Risk:** Deleted tasks reappear after sync
   - **Fix:** Add predicate to all CachedTask queries

## Verdict

Core design is sound (decorator pattern, delta logic, indexes). Issues are implementation-specific and fixable. No architectural rework needed.

## Handoff to Backend

Both issues fixed in implementation v2. Cleared for merge.
