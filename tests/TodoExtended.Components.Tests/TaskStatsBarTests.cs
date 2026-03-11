using Bunit;
using Xunit;

namespace TodoExtended.Components.Tests;

public class TaskStatsBarTests : TestContext
{
    [Fact]
    public void Render_WhenNoTasks_RendersEmptyMarkup()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskStatsBar>(parameters => parameters
            .Add(p => p.OpenCount, 0)
            .Add(p => p.CompletedCount, 0)
        );

        // Assert
        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void Render_WithOpenCount_ShowsOpenChipWithDefaultLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskStatsBar>(parameters => parameters
            .Add(p => p.OpenCount, 3)
            .Add(p => p.CompletedCount, 0)
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("3", markup);
        Assert.Contains("open", markup);
    }

    [Fact]
    public void Render_WithCustomOpenLabel_ShowsChipWithCustomLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskStatsBar>(parameters => parameters
            .Add(p => p.OpenCount, 5)
            .Add(p => p.CompletedCount, 0)
            .Add(p => p.OpenLabel, "remaining")
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("5", markup);
        Assert.Contains("remaining", markup);
    }

    [Fact]
    public void Render_WithCompletedCount_ShowsCompletedChip()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskStatsBar>(parameters => parameters
            .Add(p => p.OpenCount, 1)
            .Add(p => p.CompletedCount, 2)
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("2", markup);
        Assert.Contains("done", markup);
    }

    [Fact]
    public void Render_WithZeroCompletedCount_HidesCompletedChip()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskStatsBar>(parameters => parameters
            .Add(p => p.OpenCount, 3)
            .Add(p => p.CompletedCount, 0)
        );

        // Assert
        var successChips = cut.FindAll(".chip-success");
        Assert.Empty(successChips);
    }

    [Fact]
    public void Render_WhenHideCompletedIsFalse_ShowsHideCompletedText()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskStatsBar>(parameters => parameters
            .Add(p => p.OpenCount, 3)
            .Add(p => p.CompletedCount, 2)
            .Add(p => p.HideCompleted, false)
        );

        // Assert
        var button = cut.Find("button");
        Assert.Contains("Hide completed", button.TextContent);
    }

    [Fact]
    public void Render_WhenHideCompletedIsTrue_ShowsShowCompletedText()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskStatsBar>(parameters => parameters
            .Add(p => p.OpenCount, 3)
            .Add(p => p.CompletedCount, 2)
            .Add(p => p.HideCompleted, true)
        );

        // Assert
        var button = cut.Find("button");
        Assert.Contains("Show completed", button.TextContent);
    }

    [Fact]
    public void ToggleButton_WhenClicked_InvokesHideCompletedChangedWithNegatedValue()
    {
        // Arrange
        bool? receivedValue = null;
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.TaskStatsBar>(parameters => parameters
            .Add(p => p.OpenCount, 3)
            .Add(p => p.CompletedCount, 2)
            .Add(p => p.HideCompleted, false)
            .Add(p => p.HideCompletedChanged, (bool value) => { receivedValue = value; })
        );

        // Act
        var button = cut.Find("button");
        button.Click();

        // Assert — currently HideCompleted=false, so callback should receive true
        Assert.NotNull(receivedValue);
        Assert.True(receivedValue!.Value);
    }
}
