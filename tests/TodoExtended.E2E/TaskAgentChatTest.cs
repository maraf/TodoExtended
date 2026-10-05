using System.Text.Json;
using Microsoft.Playwright;
using NUnit.Framework;

namespace TodoExtended.E2E;

[TestFixture]
public class TaskAgentChatTest : E2ETestBase
{
    [TestCase(390, 844, true)]
    [TestCase(1280, 800, false)]
    public async Task StopsPendingProposalAndKeepsInputVisible(int width, int height, bool dark)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync($"{BaseUrl}/auth/demo-signin");
        await Page.GotoAsync($"{BaseUrl}/chat");
        var input = Page.GetByRole(AriaRole.Textbox);
        await Expect(input).ToBeEnabledAsync();
        await Page.EvaluateAsync("dark => document.documentElement.classList.toggle('dark', dark)", dark);
        await input.FillAsync("Create a task");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send message", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Stop", Exact = true }).ClickAsync();
        await Expect(input).ToBeEnabledAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true })).ToHaveCountAsync(0);
        await input.ScrollIntoViewIfNeededAsync();
        var bounds = await input.BoundingBoxAsync();
        Assert.That(bounds, Is.Not.Null);
        Assert.That(bounds!.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(bounds.X + bounds.Width, Is.LessThanOrEqualTo(width));
        Assert.That(bounds.Y + bounds.Height, Is.LessThanOrEqualTo(height));
        Assert.That(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        await input.FillAsync("Show today's tasks");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send message", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Here are your tasks for today:", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ReviewsActionsRestoresHistoryAndStartsNewConversation()
    {
        await Page.GotoAsync($"{BaseUrl}/auth/demo-signin");
        await Page.GotoAsync($"{BaseUrl}/chat");
        await Page.EvaluateAsync("""
            () => {
                localStorage.removeItem('todoextended-agent-chat-history');
                localStorage.removeItem('todoextended-chat-history');
            }
            """);
        await Page.ReloadAsync();
        var input = Page.GetByRole(AriaRole.Textbox);
        await Expect(input).ToBeEnabledAsync();
        await input.FillAsync("Create a new task");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send message", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true })).ToBeVisibleAsync();
        await Expect(input).ToBeDisabledAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("CreateTask completed successfully (demo).", new() { Exact = true }).First).ToBeVisibleAsync();
        await Expect(input).ToBeEnabledAsync();

        await Page.ReloadAsync();
        await Expect(Page.Locator("[data-testid='proposed-action-card']")).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByText("CreateTask completed successfully (demo).", new() { Exact = true }).First).ToBeVisibleAsync();

        await input.FillAsync("Set a reminder");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send message", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reject", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Rejected. No changes made.", new() { Exact = true }).First).ToBeVisibleAsync();
        await Expect(input).ToBeEnabledAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "New conversation", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Chat with your tasks" })).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-testid='proposed-action-card']")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task RestoresLegacyProposalsAsDisplayOnly()
    {
        await Page.GotoAsync($"{BaseUrl}/auth/demo-signin");
        await Page.GotoAsync($"{BaseUrl}/chat");
        await Page.EvaluateAsync("""
            () => {
                localStorage.removeItem('todoextended-agent-chat-history');
                localStorage.setItem('todoextended-chat-history', JSON.stringify([
                    {message:{role:'user',text:'Create milk',timestamp:'2026-01-10T09:00:00Z'}},
                    {message:{role:'assistant',text:'Shopping',
                        timestamp:'2026-01-10T09:00:01Z',
                        taskListReferences:[{id:'shopping',displayName:'Shopping'}],
                        proposedActions:[{type:0,description:'Create milk',parameters:{title:'Milk'}}]}}
                ]));
            }
            """);
        await Page.ReloadAsync();
        await Expect(Page.Locator("[data-testid='proposed-action-card']")).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.Locator("main a[href='/tasks/shopping']")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Textbox)).ToBeEnabledAsync();
    }

    [Test]
    public async Task AuthenticatedAguiEndpointStreamsLifecycleTextAndProposals()
    {
        await Page.GotoAsync($"{BaseUrl}/auth/demo-signin");
        var response = await Page.APIRequest.PostAsync($"{BaseUrl}/api/chat", new()
        {
            DataObject = new
            {
                threadId = "test-thread",
                runId = "test-run",
                messages = new[] { new { id = "user-1", role = "user", content = "Create a task" } },
                tools = Array.Empty<object>(),
                context = Array.Empty<object>(),
                state = new { },
                forwardedProps = new { },
            },
        });
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Headers["content-type"], Does.StartWith("text/event-stream"));
        var body = await response.TextAsync();
        var events = body.Split('\n').Where(line => line.StartsWith("data: ", StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line[6..])).ToList();
        try
        {
            var types = events.Select(item => item.RootElement.GetProperty("type").GetString()).ToList();
            Assert.That(types.First(), Is.EqualTo("RUN_STARTED"));
            Assert.That(types, Does.Contain("TEXT_MESSAGE_CONTENT"));
            Assert.That(types, Does.Contain("TOOL_CALL_START"));
            Assert.That(types.Last(), Is.EqualTo("RUN_FINISHED"));
            Assert.That(body, Does.Contain("review_task_action"));
        }
        finally
        {
            foreach (var item in events) item.Dispose();
        }
    }
}
