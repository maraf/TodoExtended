using Bunit;
using TodoExtended.Web.Components.Shared;
using TodoExtended.Web.Services.AiChat;
using Xunit;

namespace TodoExtended.Components.Tests;

public class ChatMessageBubbleTests : TestContext
{
    [Fact]
    public void Render_AssistantMarkdown_RendersFormattedHtmlInsteadOfLiteralMarkers()
    {
        // Arrange
        var message = CreateAssistantMessage("""
            In **_Microsoft_**, your Blazor-related tasks are:

            - [ ] **sdk #Blazor Override placeholders in nested project** — due 2026-04-20
            """);

        // Act
        var cut = RenderComponent<ChatMessageBubble>(parameters => parameters
            .Add(p => p.Message, message));

        // Assert
        var formattedContent = cut.Find(".break-words");
        Assert.Contains("Microsoft", formattedContent.TextContent);
        Assert.DoesNotContain("**", formattedContent.InnerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("[ ]", formattedContent.InnerHtml, StringComparison.Ordinal);
        Assert.NotEmpty(formattedContent.QuerySelectorAll("strong"));
        Assert.NotEmpty(formattedContent.QuerySelectorAll("em"));
        Assert.Single(formattedContent.QuerySelectorAll("ul"));
        Assert.Single(formattedContent.QuerySelectorAll("li"));
        Assert.Single(formattedContent.QuerySelectorAll("input[type='checkbox'][disabled]"));
    }

    [Fact]
    public void Render_AssistantMarkdownWithTaskListReference_RendersLinkedFormattedContent()
    {
        // Arrange
        var message = CreateAssistantMessage(
            "Focus on **📋 Work** before triaging the rest.",
            [new TaskListReference("work-list", "📋 Work")]);

        // Act
        var cut = RenderComponent<ChatMessageBubble>(parameters => parameters
            .Add(p => p.Message, message));

        // Assert
        var formattedContent = cut.Find(".break-words");
        var link = formattedContent.QuerySelector("a[href='/tasks/work-list']");

        Assert.NotNull(link);
        Assert.Equal("📋 Work", link.TextContent);
        Assert.Contains(formattedContent.QuerySelectorAll("strong"), element => element.TextContent.Contains("📋 Work", StringComparison.Ordinal));
        Assert.DoesNotContain("[📋 Work](/tasks/work-list)", formattedContent.InnerHtml, StringComparison.Ordinal);
    }

    private static ChatMessage CreateAssistantMessage(string text, IReadOnlyList<TaskListReference>? refs = null) =>
        new("assistant", text, null, DateTimeOffset.Parse("2026-04-20T10:31:14Z"), refs);
}
