using Bunit;
using Xunit;

namespace TodoExtended.Components.Tests;

public class ErrorAlertTests : TestContext
{
    [Fact]
    public void Render_WhenMessageIsNull_RendersNothing()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ErrorAlert>(parameters => parameters
            .Add(p => p.Message, (string?)null)
        );

        // Assert
        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Render_WhenMessageIsEmpty_RendersNothing()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ErrorAlert>(parameters => parameters
            .Add(p => p.Message, string.Empty)
        );

        // Assert
        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Render_WhenMessageIsWhitespace_RendersNothing()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ErrorAlert>(parameters => parameters
            .Add(p => p.Message, "   ")
        );

        // Assert
        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Render_WhenMessageProvided_ShowsRoseColoredAlert()
    {
        // Arrange
        const string errorMessage = "An error occurred";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ErrorAlert>(parameters => parameters
            .Add(p => p.Message, errorMessage)
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("rose", markup.ToLower());
        Assert.Contains(errorMessage, cut.Markup);
    }

    [Fact]
    public void Render_WhenMessageProvided_ContainsWarningPrefix()
    {
        // Arrange
        const string errorMessage = "Something went wrong";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ErrorAlert>(parameters => parameters
            .Add(p => p.Message, errorMessage)
        );

        // Assert
        Assert.Contains("⚠", cut.Markup);
    }

    [Fact]
    public void Render_WhenMessageProvided_AppliesCorrectStyling()
    {
        // Arrange
        const string errorMessage = "Test error";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ErrorAlert>(parameters => parameters
            .Add(p => p.Message, errorMessage)
        );

        // Assert
        var markup = cut.Markup;
        // Check for rose background and border
        Assert.Contains("bg-rose", markup);
        Assert.Contains("border-rose", markup);
        Assert.Contains("text-rose", markup);
    }
}
