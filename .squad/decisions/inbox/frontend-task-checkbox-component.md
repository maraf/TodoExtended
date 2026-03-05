# TaskStatusCheckbox Shared Component

**Author:** Frontend  
**Date:** 2025-07-18  
**Status:** Implemented

Extracted the duplicated task-completion toggle pattern from Tasks.razor and Today.razor into a shared `TaskStatusCheckbox` component at `Components/Shared/TaskStatusCheckbox.razor`.

**Component responsibilities:** checkbox/spinner toggle UI, `ITodoService.UpdateTaskStatusAsync` API call, `MicrosoftIdentityWebChallengeUserException` auth redirect, error communication via `OnError` callback.

**Parent responsibilities:** optimistic list update via `OnStatusChanged` callback (called twice on error — once for optimistic update, once for rollback), page-level error alert display via `_toggleError`.

**Parameters:** `TaskId`, `ListId`, `IsCompleted`, `TaskTitle` (all `[EditorRequired]`), `OnStatusChanged` (`EventCallback<bool>`), `OnError` (`EventCallback<string>`).

Each component instance manages its own `_isToggling` state independently, which is safe because Blazor Server processes events sequentially within a circuit.
