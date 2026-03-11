using Bunit;
using Xunit;

namespace TodoExtended.Components.Tests;

public class EmptyStateTests : TestContext
{
    [Fact]
    public void Render_DisplaysEmoji()
    {
        // Arrange
        const string emoji = "📋";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.EmptyState>(parameters => parameters
            .Add(p => p.Emoji, emoji)
            .Add(p => p.Heading, "Empty")
            .Add(p => p.Description, "No items")
        );

        // Assert
        Assert.Contains(emoji, cut.Markup);
    }

    [Fact]
    public void Render_DisplaysHeading()
    {
        // Arrange
        const string heading = "No tasks yet";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.EmptyState>(parameters => parameters
            .Add(p => p.Emoji, "✅")
            .Add(p => p.Heading, heading)
            .Add(p => p.Description, "Start by creating a task")
        );

        // Assert
        var headingElement = cut.Find("h3");
        Assert.Contains(heading, headingElement.TextContent);
    }

    [Fact]
    public void Render_DisplaysDescription()
    {
        // Arrange
        const string description = "Create your first item to get started";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.EmptyState>(parameters => parameters
            .Add(p => p.Emoji, "📝")
            .Add(p => p.Heading, "Empty list")
            .Add(p => p.Description, description)
        );

        // Assert
        Assert.Contains(description, cut.Markup);
    }

    [Fact]
    public void Render_WhenActionLabelProvided_ShowsActionButton()
    {
        // Arrange
        const string actionLabel = "Create First Template";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.EmptyState>(parameters => parameters
            .Add(p => p.Emoji, "📝")
            .Add(p => p.Heading, "No templates")
            .Add(p => p.Description, "Get started")
            .Add(p => p.ActionLabel, actionLabel)
            .Add(p => p.OnAction, () => { })
        );

        // Assert
        var button = cut.Find("button");
        Assert.Contains(actionLabel, button.TextContent);
    }

    [Fact]
    public void Render_WhenActionLabelIsNull_DoesNotShowButton()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.EmptyState>(parameters => parameters
            .Add(p => p.Emoji, "✅")
            .Add(p => p.Heading, "All done")
            .Add(p => p.Description, "Nothing to show")
            .Add(p => p.ActionLabel, (string?)null)
        );

        // Assert
        var buttons = cut.FindAll("button");
        Assert.Empty(buttons);
    }

    [Fact]
    public void ActionButton_WhenClicked_InvokesOnAction()
    {
        // Arrange
        var actionCalled = false;
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.EmptyState>(parameters => parameters
            .Add(p => p.Emoji, "📝")
            .Add(p => p.Heading, "Empty")
            .Add(p => p.Description, "Description")
            .Add(p => p.ActionLabel, "Create")
            .Add(p => p.OnAction, () => { actionCalled = true; })
        );

        // Act
        var button = cut.Find("button");
        button.Click();

        // Assert
        Assert.True(actionCalled);
    }

    [Fact]
    public void Render_AppliesCenteredStyling()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.EmptyState>(parameters => parameters
            .Add(p => p.Emoji, "🎯")
            .Add(p => p.Heading, "Center Test")
            .Add(p => p.Description, "Should be centered")
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("items-center", markup);
        Assert.Contains("justify-center", markup);
        Assert.Contains("text-center", markup);
    }
}
