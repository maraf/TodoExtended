using System.Security.Claims;
using System.Collections.Concurrent;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TodoExtended.Web.Components.Pages;
using TodoExtended.Web.Services;
using Xunit;

namespace TodoExtended.Components.Tests;

public class TagTasksTests : TestContext
{
    [Fact]
    public void Render_HidesCompletedTasksByDefault()
    {
        RegisterServices(
        [
            new TodoTaskWithList("open-1", "Open task", null, false, null, null, "list-1", "Inbox"),
            new TodoTaskWithList("done-1", "Done task", null, true, null, null, "list-1", "Inbox")
        ]);

        var cut = RenderTagTasks("home");

        cut.WaitForAssertion(() =>
        {
            var markup = cut.Markup;
            Assert.Contains("Open task", markup);
            Assert.DoesNotContain("Done task", markup);
            Assert.Contains("Show completed", markup);
        });
    }

    [Fact]
    public void Render_WhenOnlyCompletedTasksExist_ShowsRevealAction()
    {
        RegisterServices(
        [
            new TodoTaskWithList("done-1", "Done task", null, true, null, null, "list-1", "Inbox")
        ]);

        var cut = RenderTagTasks("home");

        cut.WaitForAssertion(() =>
        {
            var markup = cut.Markup;
            Assert.Contains("All tasks completed!", markup);
            Assert.DoesNotContain("Done task", markup);
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Show completed tasks", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() => Assert.Contains("Done task", cut.Markup));
    }

    [Fact]
    public void Render_WhenTagChangesDuringLoad_ShowsLatestTagTasks()
    {
        var tagService = new ControlledTagService();
        RegisterServices(tagService);

        var cut = RenderTagTasks("home");
        tagService.WaitForCall("home");

        cut.FindComponent<TagTasks>()
            .SetParametersAndRender(parameters => parameters.Add(p => p.Tag, "work"));

        tagService.WaitForCall("work");

        tagService.Complete("work",
        [
            new TodoTaskWithList("work-1", "Work task", null, false, null, null, "list-2", "Office")
        ]);
        tagService.Complete("home",
        [
            new TodoTaskWithList("home-1", "Home task", null, false, null, null, "list-1", "Inbox")
        ]);

        cut.WaitForAssertion(() =>
        {
            var markup = cut.Markup;
            Assert.Contains("Work task", markup);
            Assert.DoesNotContain("Home task", markup);
        });
    }

    private void RegisterServices(IReadOnlyList<TodoTaskWithList> tasks)
    {
        RegisterServices(new StubTagService(tasks));
    }

    private void RegisterServices(ITagService tagService)
    {
        Services.AddSingleton(tagService);
        Services.AddSingleton<ITodoService>(new StubTodoService());
        Services.AddSingleton<INotificationService>(new StubNotificationService());
    }

    private IRenderedComponent<CascadingValue<Task<AuthenticationState>>> RenderTagTasks(string tag)
    {
        var authState = Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"))));

        return RenderComponent<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(p => p.Value, authState)
            .AddChildContent<TagTasks>(child => child.Add(p => p.Tag, tag)));
    }

    private sealed class StubTagService(IReadOnlyList<TodoTaskWithList> tasks) : ITagService
    {
        public Task<IReadOnlyList<TagWithCount>> GetTagsAsync(string userId) =>
            Task.FromResult<IReadOnlyList<TagWithCount>>([]);

        public Task<IReadOnlyList<TodoTaskWithList>> GetTasksByTagAsync(string tag, string userId, bool onlyUncompleted = false) =>
            Task.FromResult(tasks);

        public Task<IReadOnlyList<string>> GetPinnedTagsAsync(string userId) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetTagPinnedAsync(string tag, bool pinned, string userId) => Task.CompletedTask;
    }

    private sealed class ControlledTagService : ITagService
    {
        private readonly ConcurrentQueue<string> _calls = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IReadOnlyList<TodoTaskWithList>>> _pending =
            new(StringComparer.Ordinal);

        public Task<IReadOnlyList<TagWithCount>> GetTagsAsync(string userId) =>
            Task.FromResult<IReadOnlyList<TagWithCount>>([]);

        public Task<IReadOnlyList<TodoTaskWithList>> GetTasksByTagAsync(string tag, string userId, bool onlyUncompleted = false)
        {
            _calls.Enqueue(tag);
            return _pending.GetOrAdd(tag, static _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }

        public Task<IReadOnlyList<string>> GetPinnedTagsAsync(string userId) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task SetTagPinnedAsync(string tag, bool pinned, string userId) => Task.CompletedTask;

        public void WaitForCall(string tag)
        {
            var success = SpinWait.SpinUntil(() => _calls.Contains(tag), TimeSpan.FromSeconds(2));
            Assert.True(success, $"Expected GetTasksByTagAsync to be called for tag '{tag}'.");
        }

        public void Complete(string tag, IReadOnlyList<TodoTaskWithList> tasks)
        {
            var completionSource = _pending.GetOrAdd(tag, static _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
            Assert.True(completionSource.TrySetResult(tasks), $"Tag '{tag}' request was already completed.");
        }
    }

    private sealed class StubTodoService : ITodoService
    {
        public Task<IReadOnlyList<TodoTaskList>> GetTaskListsAsync(string userId) =>
            Task.FromResult<IReadOnlyList<TodoTaskList>>([]);

        public Task<IReadOnlyList<TodoTask>> GetTasksAsync(string taskListId, string userId) =>
            Task.FromResult<IReadOnlyList<TodoTask>>([]);

        public Task<TodoTask?> GetTaskAsync(string taskListId, string taskId, string userId) =>
            Task.FromResult<TodoTask?>(null);

        public Task<IReadOnlyList<TodoTaskWithList>> GetTodayTasksAsync(string userId) =>
            Task.FromResult<IReadOnlyList<TodoTaskWithList>>([]);

        public Task<IReadOnlyList<TodoTaskWithList>> GetTomorrowTasksAsync(string userId) =>
            Task.FromResult<IReadOnlyList<TodoTaskWithList>>([]);

        public Task<IReadOnlyList<TodoTaskWithList>> SearchTasksAsync(string query, string userId) =>
            Task.FromResult<IReadOnlyList<TodoTaskWithList>>([]);

        public Task<IReadOnlyList<TodoTaskList>> SearchTaskListsAsync(string query, string userId) =>
            Task.FromResult<IReadOnlyList<TodoTaskList>>([]);

        public Task<TodoTask> CreateTaskAsync(string taskListId, string title, DateOnly? dueDate, string userId, TimeOnly? reminderTime = null) =>
            throw new NotSupportedException();

        public Task UpdateTaskStatusAsync(string taskListId, string taskId, bool completed, string userId) =>
            Task.CompletedTask;

        public Task SetTaskDueDateAsync(string taskListId, string taskId, DateOnly dueDate, string userId) =>
            Task.CompletedTask;

        public Task SetTaskReminderAsync(string taskListId, string taskId, DateOnly reminderDate, TimeOnly reminderTime, string userId) =>
            Task.CompletedTask;

        public Task SetTaskListSyncedAsync(string taskListId, bool isSynced, string userId) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<TodoTaskList>> GetNotSyncedTaskListsAsync(string userId) =>
            Task.FromResult<IReadOnlyList<TodoTaskList>>([]);

        public Task EnsureAllListsSyncedAsync(string userId) => Task.CompletedTask;
    }

    private sealed class StubNotificationService : INotificationService
    {
        public event Action? Changed;

        public IReadOnlyList<NotifyItem> Items => [];

        public void Add(string message, NotifySeverity severity = NotifySeverity.Info) => Changed?.Invoke();

        public void Dismiss(Guid id)
        {
        }

        public void PurgeExpired()
        {
        }
    }
}
