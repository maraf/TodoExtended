using Microsoft.Playwright;
using NUnit.Framework;

namespace TodoExtended.E2E;

/// <summary>
/// E2E tests verifying the TimePicker component in the Template edit dialog.
/// Requires the app running with Demo__Enabled=true.
/// </summary>
[TestFixture]
public class TimePickerTest : E2ETestBase
{

    [SetUp]
    public async Task SignInAsync()
    {
        // Navigate directly to the demo sign-in endpoint.  The endpoint sets the auth cookie
        // and redirects to "/"; GotoAsync follows the redirect automatically.
        // DOMContentLoaded avoids the 25+ second wait for external resources (e.g. Google
        // Fonts) that can time-out on a cold Android emulator.
        await Page.GotoAsync($"{BaseUrl}/auth/demo-signin",
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "User menu" }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    /// <summary>
    /// Opens the Edit Template dialog for the "Morning exercise" demo template,
    /// which has Due Today enabled and a 07:00 reminder time.
    /// </summary>
    private async Task OpenMorningExerciseEditDialogAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/templates", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Templates", Level = 1 })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });

        // Wait for the Blazor Server WebSocket circuit to connect so @onclick handlers work
        await Page.WaitForTimeoutAsync(3_000);

        // Find the card that contains both "Morning exercise" heading and an Edit button.
        // Multiple ancestor divs will pass both filters; .Last returns the innermost one (the card itself).
        var morningExerciseCard = Page.Locator("div")
            .Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Morning exercise", Level = 3 }) })
            .Filter(new() { Has = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }) })
            .Last;
        await morningExerciseCard.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();

        await Page.GetByRole(AriaRole.Heading, new() { Name = "Edit Template", Level = 2 })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
        await Page.WaitForTimeoutAsync(300);
    }

    [Test]
    public async Task TimePicker_ExistingTime_DisplayedOnDialogOpen()
    {
        await OpenMorningExerciseEditDialogAsync();

        // "Morning exercise" demo template has a 07:00 reminder — the trigger button shows it
        await Expect(Page.Locator("button:has-text('07:00')").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task TimePicker_SelectTime_UpdatesDisplayedValue()
    {
        await OpenMorningExerciseEditDialogAsync();

        // Open the picker
        await Page.Locator("button:has-text('07:00')").First.ClickAsync();

        // Step 1 — hour grid
        await Expect(Page.Locator("p:has-text('Select Hour')"))
            .ToBeVisibleAsync(new() { Timeout = 3_000 });

        // Pick hour 14
        await Page.Locator("button:has-text('14')").First.ClickAsync();

        // Step 2 — minute grid; back button shows the pending hour
        await Expect(Page.Locator("button:has-text('14:__')"))
            .ToBeVisibleAsync(new() { Timeout = 3_000 });
        await Expect(Page.Locator("p:has-text('Minute')")).ToBeVisibleAsync();

        // Pick minute 30
        await Page.Locator("button:has-text('30')").First.ClickAsync();

        // Picker closes and trigger button shows the new value
        await Expect(Page.Locator("button:has-text('14:30')"))
            .ToBeVisibleAsync(new() { Timeout = 3_000 });
        await Expect(Page.Locator("p:has-text('Select Hour')")).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task TimePicker_BackButton_ReturnsToHourGrid()
    {
        await OpenMorningExerciseEditDialogAsync();

        // Open picker and advance to minute step for hour 10
        await Page.Locator("button:has-text('07:00')").First.ClickAsync();
        await Expect(Page.Locator("p:has-text('Select Hour')"))
            .ToBeVisibleAsync(new() { Timeout = 3_000 });
        await Page.Locator("button:has-text('10')").First.ClickAsync();
        await Expect(Page.Locator("button:has-text('10:__')"))
            .ToBeVisibleAsync(new() { Timeout = 3_000 });

        // Click the back button (labelled "← 10:__")
        await Page.Locator("button:has-text('10:__')").ClickAsync();

        // Should return to the hour grid
        await Expect(Page.Locator("p:has-text('Select Hour')"))
            .ToBeVisibleAsync(new() { Timeout = 3_000 });
        await Expect(Page.Locator("p:has-text('Minute')")).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task TimePicker_ClearButton_ResetsToEmpty()
    {
        await OpenMorningExerciseEditDialogAsync();

        // Open picker — Clear is present because a time (07:00) is already set
        await Page.Locator("button:has-text('07:00')").First.ClickAsync();
        var clearButton = Page.Locator("button:has-text('Clear')");
        await Expect(clearButton).ToBeVisibleAsync(new() { Timeout = 3_000 });
        await clearButton.ClickAsync();

        // Trigger button should revert to the placeholder text
        await Expect(Page.Locator("button:has-text('Select time…')"))
            .ToBeVisibleAsync(new() { Timeout = 3_000 });
    }
}
