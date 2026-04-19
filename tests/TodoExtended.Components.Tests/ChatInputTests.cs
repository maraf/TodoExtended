using Bunit;
using Microsoft.JSInterop;
using Xunit;

namespace TodoExtended.Components.Tests;

public class ChatInputTests : TestContext
{
    [Fact]
    public void OnAfterRender_WhenModuleImportFails_RendersTextInputAndSendButton()
    {
        // Arrange
        string? importedPath = null;
        var moduleInterop = JSInterop.SetupModule(invocation =>
        {
            if (invocation.Arguments.Count != 1 || invocation.Arguments[0] is not string path)
            {
                return false;
            }

            importedPath = path;
            return true;
        });

        moduleInterop.Setup<bool>("isSupported").SetException(new JSException("Failed to load speech support"));

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ChatInput>();

        // Assert
        cut.WaitForAssertion(() => Assert.NotNull(importedPath));
        cut.Find("textarea");
        Assert.Single(cut.FindAll("button[aria-label='Send message']"));
        Assert.Empty(cut.FindAll("button[aria-label='Start recording']"));
    }

    [Fact]
    public void OnAfterRender_WhenModuleLoads_ImportsChatInputModuleAndShowsSpeechButton()
    {
        // Arrange
        var (moduleInterop, importedPathAccessor) = SetupSpeechModule();
        moduleInterop.Setup<bool>("isSupported").SetResult(true);

        // Act
        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ChatInput>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var importedPath = importedPathAccessor();
            Assert.NotNull(importedPath);
            Assert.Contains("Components/Shared/ChatInput.", importedPath, StringComparison.Ordinal);
            Assert.EndsWith(".razor.js", importedPath, StringComparison.Ordinal);
            Assert.Single(cut.FindAll("button[aria-label='Start recording']"));
        });
    }

    [Fact]
    public void ToggleListening_WhenSpeechToTextStarts_ShowsActiveMicrophoneState()
    {
        // Arrange
        var (moduleInterop, _) = SetupSpeechModule();
        moduleInterop.Setup<bool>("isSupported").SetResult(true);
        moduleInterop.SetupVoid("startListening", _ => true);

        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ChatInput>();
        cut.WaitForElement("button[aria-label='Start recording']");

        // Act
        cut.Find("button[aria-label='Start recording']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var button = cut.Find("button[aria-label='Stop recording']");
            Assert.True(button.HasAttribute("aria-pressed"));
            Assert.Contains("ring-brand-400", button.ClassList);
            Assert.Single(button.GetElementsByTagName("svg"));
            Assert.DoesNotContain("animate-ping", button.InnerHtml, StringComparison.Ordinal);
            Assert.Contains("text-brand-600", button.GetElementsByTagName("svg")[0].ClassList);
        });
    }

    [Fact]
    public async Task OnSpeechEnded_WhenSpeechToTextStops_RestoresIdleMicrophoneState()
    {
        // Arrange
        var (moduleInterop, _) = SetupSpeechModule();
        moduleInterop.Setup<bool>("isSupported").SetResult(true);
        moduleInterop.SetupVoid("startListening", _ => true);

        var cut = RenderComponent<TodoExtended.Web.Components.Shared.ChatInput>();
        cut.WaitForElement("button[aria-label='Start recording']");
        cut.Find("button[aria-label='Start recording']").Click();
        cut.WaitForElement("button[aria-label='Stop recording']");

        // Act
        await cut.InvokeAsync(() =>
        {
            cut.Instance.OnSpeechEnded();
            return Task.CompletedTask;
        });

        // Assert
        cut.WaitForAssertion(() =>
        {
            var button = cut.Find("button[aria-label='Start recording']");
            Assert.False(button.HasAttribute("aria-pressed"));
            Assert.DoesNotContain("ring-brand-400", button.ClassList);
            Assert.Single(button.GetElementsByTagName("svg"));
            Assert.DoesNotContain("animate-ping", button.InnerHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("text-brand-600", button.GetElementsByTagName("svg")[0].ClassList);
        });
    }

    private (BunitJSModuleInterop moduleInterop, Func<string?> importedPathAccessor) SetupSpeechModule()
    {
        string? importedPath = null;
        var moduleInterop = JSInterop.SetupModule(invocation =>
        {
            if (invocation.Arguments.Count != 1 || invocation.Arguments[0] is not string path)
            {
                return false;
            }

            importedPath = path;
            return true;
        });

        return (moduleInterop, () => importedPath);
    }
}
