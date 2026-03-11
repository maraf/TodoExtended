using Bunit;
using Xunit;

namespace TodoExtended.Components.Tests;

public class TaskListSkeletonTests : TestContext
{
    [Fact]
    public void Render_WithDefaults_RendersFiveSkeletonRows()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskListSkeleton>();

        // Assert
        var rows = cut.FindAll(".task-row");
        Assert.Equal(5, rows.Count);
    }

    [Fact]
    public void Render_WithCustomRowCount_RendersCorrectNumberOfRows()
    {
        // Arrange
        const int expectedCount = 3;

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskListSkeleton>(parameters => parameters
            .Add(p => p.RowCount, expectedCount)
        );

        // Assert
        var rows = cut.FindAll(".task-row");
        Assert.Equal(expectedCount, rows.Count);
    }

    [Fact]
    public void Render_WithCustomGradientClasses_AppliesClassesToTopBar()
    {
        // Arrange
        const string customGradient = "from-pink-400 via-red-500 to-orange-400";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskListSkeleton>(parameters => parameters
            .Add(p => p.GradientClasses, customGradient)
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("from-pink-400", markup);
        Assert.Contains("via-red-500", markup);
        Assert.Contains("to-orange-400", markup);
    }

    [Fact]
    public void Render_ShowBadgeSkeletonFalse_DoesNotRenderBadgePlaceholder()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskListSkeleton>(parameters => parameters
            .Add(p => p.ShowBadgeSkeleton, false)
        );

        // Assert
        var badgePlaceholders = cut.FindAll(".w-16");
        Assert.Empty(badgePlaceholders);
    }

    [Fact]
    public void Render_ShowBadgeSkeletonTrue_RendersBadgePlaceholders()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskListSkeleton>(parameters => parameters
            .Add(p => p.ShowBadgeSkeleton, true)
            .Add(p => p.RowCount, 3)
        );

        // Assert
        var badgePlaceholders = cut.FindAll(".w-16");
        Assert.True(badgePlaceholders.Count > 0, "Expected badge placeholders when ShowBadgeSkeleton is true");
    }

    [Fact]
    public void Render_CardWrapper_HasCorrectClasses()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskListSkeleton>();

        // Assert
        var card = cut.Find(".card");
        var markup = cut.Markup;
        Assert.Contains("dark:", markup);
    }

    [Fact]
    public void Render_AllSkeletonElements_HaveAnimatePulseClass()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskListSkeleton>(parameters => parameters
            .Add(p => p.RowCount, 3)
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("animate-pulse", markup);
    }
}
