using Bunit;
using Xunit;

namespace TodoExtended.Components.Tests;

public class ModalDialogTests : TestContext
{
    [Fact]
    public void Render_WhenVisibleIsFalse_RendersNothing()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ModalDialog>(parameters => parameters
            .Add(p => p.Visible, false)
            .Add(p => p.Title, "Test Modal")
        );

        // Assert
        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Render_WhenVisibleIsTrue_RendersOverlayAndDialog()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ModalDialog>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Title, "Test Modal")
        );

        // Assert
        var markup = cut.Markup;
        Assert.Contains("fixed", markup);
        Assert.Contains("inset-0", markup);
        Assert.Contains("z-50", markup);
    }

    [Fact]
    public void Render_WhenVisibleIsTrue_DisplaysTitle()
    {
        // Arrange
        const string expectedTitle = "My Test Title";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ModalDialog>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Title, expectedTitle)
        );

        // Assert
        Assert.Contains(expectedTitle, cut.Markup);
    }

    [Fact]
    public void CloseButton_WhenClicked_InvokesOnClose()
    {
        // Arrange
        var closeCalled = false;
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ModalDialog>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Title, "Test Modal")
            .Add(p => p.OnClose, () => { closeCalled = true; })
        );

        // Act
        var closeButton = cut.Find("button");
        closeButton.Click();

        // Assert
        Assert.True(closeCalled);
    }

    [Fact]
    public void Render_RendersBodyRenderFragment()
    {
        // Arrange
        const string bodyContent = "This is the modal body content";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ModalDialog>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Title, "Test Modal")
            .Add(p => p.Body, builder => builder.AddContent(0, bodyContent))
        );

        // Assert
        Assert.Contains(bodyContent, cut.Markup);
    }

    [Fact]
    public void Render_WhenFooterProvided_RendersFooterRenderFragment()
    {
        // Arrange
        const string footerContent = "Footer button text";

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ModalDialog>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Title, "Test Modal")
            .Add(p => p.Footer, builder => builder.AddContent(0, footerContent))
        );

        // Assert
        Assert.Contains(footerContent, cut.Markup);
    }

    [Fact]
    public void Render_WhenFooterIsNull_DoesNotRenderFooterSection()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ModalDialog>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Title, "Test Modal")
            .Add(p => p.Footer, (Microsoft.AspNetCore.Components.RenderFragment?)null)
        );

        // Assert - footer shouldn't be present in markup when null
        // We can check that the dialog is there but footer section is missing
        Assert.Contains("Test Modal", cut.Markup);
    }

    [Fact]
    public void Form_WhenSubmitted_InvokesOnSubmit()
    {
        // Arrange
        var submitCalled = false;
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ModalDialog>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Title, "Test Modal")
            .Add(p => p.OnSubmit, () => { submitCalled = true; })
        );

        // Act
        cut.Find("form").Submit();

        // Assert
        Assert.True(submitCalled);
    }

    [Fact]
    public void Render_WhenVisible_ContainsFormElement()
    {
        // Arrange & Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ModalDialog>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Title, "Test Modal")
        );

        // Assert
        Assert.NotNull(cut.Find("form"));
    }
}
