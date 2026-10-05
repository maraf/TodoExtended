using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TodoExtended.Web.Components.Pages;
using TodoExtended.Web.Components.Shared;
using TodoExtended.Web.Services.AiChat;
using Xunit;

namespace TodoExtended.Components.Tests;

public class ChatTimestampRenderingTests : TestContext
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoredPageRerenderPreservesUserAndAssistantBubbleTimestamps(bool legacy)
    {
        Services.AddLogging();
        Services.AddSingleton<IChatService, StubChatService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(_ => true);
        var userTime = DateTimeOffset.Parse("2026-01-10T09:00:00+02:00");
        var assistantTime = userTime.AddMinutes(1);
        var thread = new BrowserConversationThread();
        thread.LoadLegacy("""
            [{"message":{"role":"user","text":"Hello","timestamp":"2026-01-10T09:00:00+02:00"}},
             {"message":{"role":"assistant","text":"Hi","timestamp":"2026-01-10T09:01:00+02:00"}}]
            """);
        JSInterop.Setup<string?>("localStorage.getItem", invocation =>
            Equals(invocation.Arguments[0], "todoextended-agent-chat-history"))
            .SetResult(legacy ? null : thread.Serialize());
        JSInterop.Setup<string?>("localStorage.getItem", invocation =>
            Equals(invocation.Arguments[0], "todoextended-chat-history"))
            .SetResult("""
                [{"message":{"role":"user","text":"Hello","timestamp":"2026-01-10T09:00:00+02:00"}},
                 {"message":{"role":"assistant","text":"Hi","timestamp":"2026-01-10T09:01:00+02:00"}}]
                """);

        var cut = RenderComponent<Chat>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindComponents<ChatMessageBubble>().Count));
        var bubbles = cut.FindComponents<ChatMessageBubble>();
        Assert.Equal([userTime, assistantTime], bubbles.Select(bubble => bubble.Instance.Message.Timestamp));
        var displayedTimes = bubbles.Select(bubble => bubble.Find("p.text-\\[10px\\]").TextContent).ToList();

        cut.Render();

        bubbles = cut.FindComponents<ChatMessageBubble>();
        Assert.Equal([userTime, assistantTime], bubbles.Select(bubble => bubble.Instance.Message.Timestamp));
        Assert.Equal(displayedTimes, bubbles.Select(bubble => bubble.Find("p.text-\\[10px\\]").TextContent));
    }
}
