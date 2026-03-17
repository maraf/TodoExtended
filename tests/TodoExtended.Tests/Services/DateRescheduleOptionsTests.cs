using TodoExtended.Web.Services;

namespace TodoExtended.Tests.Services;

public class DateRescheduleOptionsTests
{
    // Helper: build and return the labels
    private static List<string> Labels(DateOnly today, bool includeToday) =>
        DateRescheduleOptions.Build(today, includeToday).Select(o => o.Label).ToList();

    private static List<DateOnly> Dates(DateOnly today, bool includeToday) =>
        DateRescheduleOptions.Build(today, includeToday).Select(o => o.Date).ToList();

    // ── includeToday flag ────────────────────────────────────────────────────

    [Fact]
    public void Build_IncludeTodayTrue_FirstOptionIsToday()
    {
        var today = new DateOnly(2026, 3, 17); // Tuesday
        var options = DateRescheduleOptions.Build(today, includeToday: true);
        Assert.Equal("Today", options[0].Label);
        Assert.Equal(today, options[0].Date);
    }

    [Fact]
    public void Build_IncludeTodayFalse_DoesNotContainTodayOption()
    {
        var today = new DateOnly(2026, 3, 17); // Tuesday
        var labels = Labels(today, includeToday: false);
        Assert.DoesNotContain("Today", labels);
    }

    // ── Monday ───────────────────────────────────────────────────────────────

    [Fact]
    public void Build_Monday_TomorrowIsTuesdayAndRemainingWeekDaysFollowThroughSunday()
    {
        var today = new DateOnly(2026, 3, 16); // Monday
        var options = DateRescheduleOptions.Build(today, includeToday: false);

        // Tomorrow = Tuesday
        Assert.Equal("Tomorrow", options[0].Label);
        Assert.Equal(today.AddDays(1), options[0].Date);

        // Should have Tue through Sun (5 more days after tomorrow)
        var labels = options.Select(o => o.Label).ToList();
        Assert.Contains("Wed 18", labels);
        Assert.Contains("Thu 19", labels);
        Assert.Contains("Fri 20", labels);
        Assert.Contains("Sat 21", labels);
        Assert.Contains("Sun 22", labels);

        // Next week = following Monday
        Assert.Equal("Next week", labels.Last());
        Assert.Equal(today.AddDays(7), options.Last().Date);
    }

    [Fact]
    public void Build_Monday_NextWeekIsNotDuplicateOfTomorrow()
    {
        var today = new DateOnly(2026, 3, 16); // Monday
        var options = DateRescheduleOptions.Build(today, includeToday: false);
        var tomorrowDate = today.AddDays(1);
        var nextWeekDate = options.Last().Date;
        Assert.NotEqual(tomorrowDate, nextWeekDate);
    }

    // ── Saturday ─────────────────────────────────────────────────────────────

    [Fact]
    public void Build_Saturday_TomorrowIsSundayAndNoOtherWeekdays()
    {
        var today = new DateOnly(2026, 3, 21); // Saturday
        var options = DateRescheduleOptions.Build(today, includeToday: false);

        Assert.Equal("Tomorrow", options[0].Label);
        Assert.Equal(today.AddDays(1), options[0].Date); // Sunday

        // No additional weekdays between Sunday and Sunday (loop body doesn't run)
        var labels = options.Select(o => o.Label).ToList();
        Assert.DoesNotContain("Mon 23", labels);
    }

    [Fact]
    public void Build_Saturday_NextWeekIsMonday()
    {
        var today = new DateOnly(2026, 3, 21); // Saturday
        var options = DateRescheduleOptions.Build(today, includeToday: false);
        var last = options.Last();
        Assert.Equal("Next week", last.Label);
        Assert.Equal(new DateOnly(2026, 3, 23), last.Date); // Next Monday
    }

    // ── Sunday ───────────────────────────────────────────────────────────────

    [Fact]
    public void Build_Sunday_TomorrowIsMondayAndNextWeekIsNotDuplicated()
    {
        var today = new DateOnly(2026, 3, 22); // Sunday
        var options = DateRescheduleOptions.Build(today, includeToday: false);

        // Tomorrow = Monday
        Assert.Equal("Tomorrow", options[0].Label);
        Assert.Equal(today.AddDays(1), options[0].Date);

        // Next week monday would also be tomorrow — so "Next week" must not appear
        var labels = options.Select(o => o.Label).ToList();
        Assert.DoesNotContain("Next week", labels);
    }

    [Fact]
    public void Build_Sunday_NoWeekdaysAfterTomorrow()
    {
        var today = new DateOnly(2026, 3, 22); // Sunday
        var options = DateRescheduleOptions.Build(today, includeToday: false);
        // The while loop should add nothing (day starts at Tuesday > thisWeekSunday which is today)
        // So the only option is "Tomorrow"
        Assert.Single(options);
        Assert.Equal("Tomorrow", options[0].Label);
    }

    // ── Dates are correct ────────────────────────────────────────────────────

    [Fact]
    public void Build_Tuesday_DatesAreCorrectlyIncremented()
    {
        var today = new DateOnly(2026, 3, 17); // Tuesday
        var options = DateRescheduleOptions.Build(today, includeToday: false);

        Assert.Equal(today.AddDays(1), options[0].Date); // Wed 18 — tomorrow
        Assert.Equal(today.AddDays(2), options[1].Date); // Thu 19
        Assert.Equal(today.AddDays(3), options[2].Date); // Fri 20
        Assert.Equal(today.AddDays(4), options[3].Date); // Sat 21
        Assert.Equal(today.AddDays(5), options[4].Date); // Sun 22
        Assert.Equal(new DateOnly(2026, 3, 23), options[5].Date); // Next Monday
    }
}
