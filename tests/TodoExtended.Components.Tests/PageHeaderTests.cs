using Bunit;
using Xunit;

namespace TodoExtended.Components.Tests;

public class PageHeaderTests : TestContext
{
    [Fact]
    public void Render_DisplaysTitleInH1()
    {
        // Arrange
        const string expectedTitle = "My Page Title";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, expectedTitle)
        );

        // Assert
        var h1 = cut.Find("h1");
        Assert.Contains(expectedTitle, h1.TextContent);
    }

    [Fact]
    public void Render_AppliesGradientClasses()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, "Test Title")
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("bg-gradient-to-br", markup);
    }

    [Fact]
    public void Render_WhenIconProvided_RendersIconContent()
    {
        // Arrange
        const string iconSvgPath = "M12 3v1m0 16v1m9-9h-1M4 12H3";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, "Test Title")
            .Add(p => p.Icon, builder => 
            {
                builder.OpenElement(0, "svg");
                builder.AddAttribute(1, "class", "w-5 h-5");
                builder.OpenElement(2, "path");
                builder.AddAttribute(3, "d", iconSvgPath);
                builder.CloseElement();
                builder.CloseElement();
            })
        );

        // Assert
        Assert.Contains(iconSvgPath, cut.Markup);
    }

    [Fact]
    public void Render_WhenIconIsNull_StillRendersTitle()
    {
        // Arrange
        const string expectedTitle = "No Icon Title";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, expectedTitle)
            .Add(p => p.Icon, (Microsoft.AspNetCore.Components.RenderFragment?)null)
        );

        // Assert
        var h1 = cut.Find("h1");
        Assert.Contains(expectedTitle, h1.TextContent);
    }

    [Fact]
    public void Render_IconContainer_HasCorrectDimensions()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, "Test Title")
            .Add(p => p.Icon, builder => builder.AddContent(0, "🎯"))
        );

        // Assert
        var markup = cut.Markup;
        // Icon container should have w-10 h-10 classes
        Assert.Contains("w-10", markup);
        Assert.Contains("h-10", markup);
    }
}
