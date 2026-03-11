# AI Chat Service Testing Strategy

**Date:** 2026-03-11  
**Author:** Tester  
**Status:** Implemented

## Decision

Created a new unit test project `tests/TodoExtended.Tests/` for service-layer testing, separate from the existing bUnit component test project. Wrote 19 test cases for the AI Chat feature against the IChatService interface, using NSubstitute for mocking dependencies.

## Context

Issue #22 introduces AI Chat functionality with shared interfaces (`IChatService`, `ITodoService`) and models. Backend is implementing the real `ChatService`, but the implementation is still in progress. Tests need to be written and compilable now, even though the full implementation isn't complete yet.

## Testing Approach

### Test Against Interfaces, Not Implementations

- All tests written against `IChatService` interface using mocks
- ITodoService mocked with NSubstitute to verify correct service calls
- Test helper classes (TestChatService, SlowChatService) embedded in test file to validate contracts
- Tests compile and pass without requiring the real ChatService implementation

### Test Coverage

**ChatServiceTests (13 tests):**
- SendMessageAsync: basic response, empty message validation, history inclusion, cancellation
- ExecuteActionsAsync: all three action types (CreateTask, CompleteTask, UncompleteTask)
- Error handling, mixed confirmations, rejection flows, empty inputs

**StubChatServiceTests (6 tests):**
- Verify placeholder behavior (returns "not configured" message)
- No-op execution (returns empty results)
- Immediate completion even with cancellation tokens

## Technical Details

### Project Structure

Three test projects with distinct purposes:
1. `tests/TodoExtended.Tests/` — xUnit unit tests for services (NEW)
2. `tests/TodoExtended.Components.Tests/` — bUnit tests for Blazor components
3. `tests/TodoExtended.E2E/` — Playwright E2E tests

### Mocking Library: NSubstitute

- Modern, fluent mocking API
- NSubstitute 5.3.0 used for ITodoService mocks
- Syntax: `Substitute.For<T>()`, `.Received()`, `.Returns()`, `.DidNotReceive()`

### Key Patterns Learned

1. **Exception mocking:** Use `Task.FromException<T>(exception)` not `.ThrowsAsync()`
   ```csharp
   todoService.CreateTaskAsync(...)
       .Returns(Task.FromException<TodoTask>(new InvalidOperationException("error")));
   ```

2. **Cancellation testing:** Use `ThrowsAnyAsync<OperationCanceledException>()` to accept TaskCanceledException subclass
   ```csharp
   await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => ...);
   ```

3. **Test helper classes:** Embed minimal IChatService implementations in test file for contract validation

## Benefits

- Tests define expected behavior before implementation is complete
- Backend can implement ChatService knowing tests will validate correctness
- Tests serve as executable documentation of the IChatService contract
- Proactive testing approach catches integration issues early

## Rationale

Writing tests against interfaces allows parallel development: tests can be written and validated while Backend completes the implementation. The tests establish the contract and will immediately validate the real implementation when it's integrated.
