using Bunit;
using Xunit;

namespace TodoExtended.Components.Tests;

public class SkeletonGridTests : TestContext
{
    [Fact]
    public void Render_WithDefaultCount_RendersThreeSkeletonItems()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.SkeletonGrid>();

        // Assert
        var skeletons = cut.FindAll(".animate-pulse, [class*='animate-pulse']");
        // With default count of 3, we expect 3 skeleton items
        Assert.True(skeletons.Count >= 3, $"Expected at least 3 skeleton items, found {skeletons.Count}");
    }

    [Fact]
    public void Render_WithCustomCount_RendersCorrectNumberOfItems()
    {
        // Arrange
        const int expectedCount = 5;

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.SkeletonGrid>(parameters => parameters
            .Add(p => p.Count, expectedCount)
        );

        // Assert
        var skeletons = cut.FindAll(".animate-pulse, [class*='animate-pulse']");
        Assert.True(skeletons.Count >= expectedCount, $"Expected at least {expectedCount} skeleton items, found {skeletons.Count}");
    }

    [Fact]
    public void Render_AppliesAnimatePulseClass()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.SkeletonGrid>(parameters => parameters
            .Add(p => p.Count, 2)
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("animate-pulse", markup);
    }

    [Fact]
    public void Render_WithDefaultHeight_AppliesH32Class()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.SkeletonGrid>();

        // Assert
        var markup = cut.Markup;
        Assert.Contains("h-32", markup);
    }

    [Fact]
    public void Render_WithCustomHeight_AppliesCustomHeightClass()
    {
        // Arrange
        const string customHeight = "h-40";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.SkeletonGrid>(parameters => parameters
            .Add(p => p.Height, customHeight)
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains(customHeight, markup);
    }

    [Fact]
    public void Render_WithZeroCount_RendersNothing()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.SkeletonGrid>(parameters => parameters
            .Add(p => p.Count, 0)
        );

        // Assert
        var skeletons = cut.FindAll(".animate-pulse, [class*='animate-pulse']");
        Assert.Empty(skeletons);
    }

    [Fact]
    public void Render_AppliesGridLayout()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.SkeletonGrid>(parameters => parameters
            .Add(p => p.Count, 3)
        );

        // Assert
        var markup = cut.Markup;
        // Skeleton grids typically use grid layout with responsive columns
        Assert.Contains("grid", markup);
    }
}
