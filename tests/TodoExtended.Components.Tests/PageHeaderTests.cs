using Bunit;
using Xunit;

namespace TodoExtended.Components.Tests;

/// <summary>
/// PageHeader renders inside SectionContent which requires a SectionOutlet
/// in the render tree. bUnit doesn't natively support Blazor sections, so
/// these tests verify component parameters and rendering without exceptions.
/// Full visual integration is validated by E2E/screenshot tests.
/// </summary>
public class PageHeaderTests : TestContext
{
    [Fact]
    public void Render_WithTitle_DoesNotThrow()
    {
        // PageHeader uses SectionContent — content only renders with a SectionOutlet.
        // We verify the component instantiates and accepts parameters without error.
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, "My Page Title")
        );

        Assert.NotNull(cut.Instance);
        Assert.Equal("My Page Title", cut.Instance.Title);
    }

    [Fact]
    public void Render_AcceptsGradientParameters()
    {
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, "Test Title")
            .Add(p => p.FromColor, "from-amber-400")
            .Add(p => p.ToColor, "to-orange-500")
            .Add(p => p.ShadowColor, "shadow-amber-500/20")
        );

        Assert.Equal("from-amber-400", cut.Instance.FromColor);
        Assert.Equal("to-orange-500", cut.Instance.ToColor);
        Assert.Equal("shadow-amber-500/20", cut.Instance.ShadowColor);
    }

    [Fact]
    public void Render_WithIcon_DoesNotThrow()
    {
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, "Test Title")
            .Add(p => p.Icon, builder =>
            {
                builder.OpenElement(0, "svg");
                builder.AddAttribute(1, "class", "w-5 h-5");
                builder.CloseElement();
            })
        );

        Assert.NotNull(cut.Instance);
        Assert.NotNull(cut.Instance.Icon);
    }

    [Fact]
    public void Render_WithoutIcon_DoesNotThrow()
    {
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, "No Icon Title")
        );

        Assert.NotNull(cut.Instance);
        Assert.Null(cut.Instance.Icon);
    }

    [Fact]
    public void Render_DefaultColors_AreSet()
    {
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.PageHeader>(parameters => parameters
            .Add(p => p.Title, "Test Title")
        );

        Assert.Equal("from-brand-500", cut.Instance.FromColor);
        Assert.Equal("to-violet-600", cut.Instance.ToColor);
        Assert.Equal("shadow-brand-500/20", cut.Instance.ShadowColor);
    }
}
