using Bunit;
using Xunit;

namespace TodoExtended.Components.Tests;

public class FloatingFieldTests : TestContext
{
    [Fact]
    public void Render_DisplaysLabel()
    {
        // Arrange
        const string label = "Task Title";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.FloatingField>(parameters => parameters
            .Add(p => p.Label, label)
            .Add(p => p.Value, "")
        );

        // Assert
        var labelElement = cut.Find("label");
        Assert.Contains(label, labelElement.TextContent);
    }

    [Fact]
    public void Render_DisplaysInputWithValue()
    {
        // Arrange
        const string value = "My Task";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.FloatingField>(parameters => parameters
            .Add(p => p.Label, "Title")
            .Add(p => p.Value, value)
        );

        // Assert
        var input = cut.Find("input");
        Assert.Equal(value, input.GetAttribute("value"));
    }

    [Fact]
    public void Render_AppliesTypeAttribute()
    {
        // Arrange
        const string inputType = "email";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.FloatingField>(parameters => parameters
            .Add(p => p.Label, "Email")
            .Add(p => p.Value, "")
            .Add(p => p.Type, inputType)
        );

        // Assert
        var input = cut.Find("input");
        Assert.Equal(inputType, input.GetAttribute("type"));
    }

    [Fact]
    public void Render_WithDefaultType_UsesText()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.FloatingField>(parameters => parameters
            .Add(p => p.Label, "Name")
            .Add(p => p.Value, "")
        );

        // Assert
        var input = cut.Find("input");
        Assert.Equal("text", input.GetAttribute("type"));
    }

    [Fact]
    public void Input_WhenValueChanges_PropagatesValueChanged()
    {
        // Arrange
        string? changedValue = null;
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.FloatingField>(parameters => parameters
            .Add(p => p.Label, "Title")
            .Add(p => p.Value, "Initial")
            .Add(p => p.ValueChanged, newValue => { changedValue = newValue; })
        );

        // Act
        var input = cut.Find("input");
        input.Input("Updated Value");

        // Assert
        Assert.Equal("Updated Value", changedValue);
    }

    [Fact]
    public void Render_HasFloatingLabelPattern()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.FloatingField>(parameters => parameters
            .Add(p => p.Label, "Email Address")
            .Add(p => p.Value, "")
        );

        // Assert
        var markup = cut.Markup;
        // Floating label pattern typically uses specific classes
        Assert.Contains("floating", markup.ToLower());
    }

    [Fact]
    public void Render_InputHasPlaceholder()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.FloatingField>(parameters => parameters
            .Add(p => p.Label, "Username")
            .Add(p => p.Value, "")
        );

        // Assert
        var input = cut.Find("input");
        // Floating fields typically use a space placeholder for the CSS pattern
        Assert.NotNull(input.GetAttribute("placeholder"));
    }
}
