# Hockney — Runner / DevOps

## Project Context

- **Project:** TodoExtended — Alternative view for Microsoft To Do with extended features
- **Stack:** .NET 10, Blazor Web App, Interactive Server, Microsoft Identity + Graph API
- **User:** Marek Fišera
- **App path:** src/TodoExtended.Web
- **Default URL:** http://localhost:5016

## Learnings

- App starts successfully with `dotnet watch` and is responsive on http://localhost:5016 (redirects to https://localhost:7065)
- HTTPS endpoint responds with HTTP/2 on port 7065
- dotnet watch process detaches cleanly and runs in background without issues
- Compilation and hot-reload are operational


## Push Sync Allowlist Rollout Review (2026-04-17)

**Status:** Complete  
**Review Outcome:** Ready for merge  
**Code Quality:** ✅ Verified

### Architecture Review

#### Design Decisions

1. **Opt-in by default** — Feature disabled, empty allowlist. Zero behavior change at merge. Admin explicitly enables + populates allowlist.
2. **Email-based allowlist** — Normalized `User.Email` from OIDC claims. Human-readable for admins. Stable across account recovery (synced on every sign-in).
3. **No schema changes** — Per-user push state stored in existing `SyncMetadata` table using JSON keys. Avoids EF migration risk late in cycle.
4. **Health-based fallback** — Unhealthy conditions silently fall back to delta sync (proven path). Safety-first, behavior-safe.
5. **Autonomous background service** — Subscription lifecycle managed independently. Resilient to transient failures.
6. **Webhook token validation** — Graph API validation tokens echoed back to complete subscription handshake. Security-first.

#### Service Layering

```
API / Middleware
    ↓
CachedTodoService → IPushSyncHealthService → [Healthy? Skip delta sync : Do delta sync]
    ↓                      ↓
PushSyncBackgroundService  PushSyncStateStore
    ↓                      ↓
IGraphTodoClient           SyncMetadata table
```

- **Clear separation** — Each service has single responsibility
- **Testable** — All services mock-able via interfaces
- **Resilient** — Failures in one service don't cascade (health check non-throwing)

### Code Quality

#### Service Implementations (7 files)

| File | Purpose | Testable | Reviewed |
|------|---------|----------|----------|
| PushSyncOptions.cs | Config POCO | ✅ | ✅ |
| PushSyncGate.cs | Allowlist check | ✅ | ✅ |
| PushSyncStateStore.cs | State persistence | ✅ | ✅ |
| PushSyncHealthService.cs | Health determination | ✅ | ✅ |
| PushSyncBackgroundService.cs | Subscription lifecycle | ✅ | ✅ |
| PushSyncWebhookModels.cs | Graph API contracts | ✅ | ✅ |
| Program.cs + webhook endpoint | DI + webhook handler | ✅ | ✅ |

#### Integration Points (6 files modified)

| File | Change | Impact | Verified |
|------|--------|--------|----------|
| Program.cs | Register services | Low (config-driven) | ✅ |
| appsettings.json | Add PushSync section | None (disabled by default) | ✅ |
| CachedTodoService.cs | Health check before skip-delta | Medium (fallback provided) | ✅ |
| IGraphTodoClient.cs | New NotificationUrl property | Low (read-only) | ✅ |
| HttpGraphTodoClient.cs | Implement NotificationUrl | Low (simple) | ✅ |
| DemoGraphTodoClient.cs | Demo NotificationUrl | None (demo only) | ✅ |
| UserSyncMiddleware.cs | Re-sync email | Low (already syncing user) | ✅ |

### Test Coverage

#### New Test Files (12 scenarios + 2 updated)

- **PushSyncGateTests:** 4 scenarios (enabled/disabled, listed/unlisted, case-sensitive)
- **PushSyncHealthServiceTests:** 8 scenarios (health state transitions, fallback triggers)
- **CachedTodoServiceTests:** Updated for integration (2 scenarios: healthy skip, unhealthy fallback)

#### Coverage Quality

✅ All decision paths tested  
✅ All allowlist edge cases (empty, case-mismatch, not-listed)  
✅ All health failures isolated (config, subscription, background, stale)  
✅ Integration happy path (healthy→skip) + fallback path (unhealthy→delta)  
✅ Mock isolation (no real Graph API calls, no real background services)

### Dependency Review

✅ No new external NuGet packages  
✅ Uses existing `IHostApplicationLifetime` for app lifecycle  
✅ Uses existing `ISyncMetadataRepository` for state storage  
✅ Uses existing `IGraphTodoClient` for Graph integration  
✅ No breaking changes to existing APIs

### Production Readiness

| Criterion | Status | Notes |
|-----------|--------|-------|
| **Default safety** | ✅ | Feature off, allowlist empty |
| **Configuration clarity** | ✅ | Opt-in explicit, documented in appsettings |
| **Fallback mechanism** | ✅ | Non-throwing, always safe (delta sync) |
| **Test coverage** | ✅ | 12 scenarios + integration |
| **Build cleanliness** | ✅ | No warnings, no errors |
| **Observability** | ✅ | Health timestamps logged to metadata |
| **Reversibility** | ✅ | Disable in config → back to all-delta |

### Recommendation

**✅ READY FOR MERGE**

- Feature is behavior-safe (disabled by default)
- Architecture is production-ready (health-based fallback)
- Test coverage is comprehensive (all paths tested)
- Code quality verified (service layering, DI, error handling)
- No schema risk (existing SyncMetadata table)
