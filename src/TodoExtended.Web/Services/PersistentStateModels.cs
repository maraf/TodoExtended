using System.Globalization;
using System.Text;

namespace TodoExtended.Web.Services;

public record PersistedTodoTask(
    string Id,
    string Title,
    bool IsCompleted,
    DateOnly? DueDate,
    string? Importance,
    bool HasReminder = false,
    bool IsRecurring = false);

public record PersistedTodoTaskWithList(
    string Id,
    string Title,
    bool IsCompleted,
    string? Importance,
    string ListId,
    string ListName,
    bool HasReminder = false,
    bool IsRecurring = false);

public record PersistedTodoTaskList(string Id, string DisplayName)
{
    public (string? Emoji, string Name) Emoji()
    {
        if (string.IsNullOrEmpty(DisplayName))
            return (null, DisplayName ?? string.Empty);

        var enumerator = StringInfo.GetTextElementEnumerator(DisplayName);
        if (!enumerator.MoveNext())
            return (null, DisplayName ?? string.Empty);

        var firstElement = (string)enumerator.Current;
        var firstRune = firstElement.EnumerateRunes().First();

        if (IsEmojiLike(firstRune))
        {
            var rest = DisplayName[firstElement.Length..].TrimStart();
            return (firstElement, rest);
        }

        return (null, DisplayName ?? string.Empty);
    }

    private static bool IsEmojiLike(Rune rune)
    {
        int v = rune.Value;
        return v >= 0x1F000 ||                    // SMP emoji blocks (emoticons, symbols, flags, etc.)
               (v >= 0x2600 && v <= 0x27BF) ||    // Misc Symbols & Dingbats
               (v >= 0x2300 && v <= 0x23FF) ||    // Misc Technical (⌚, ⏰, etc.)
               (v >= 0x2B50 && v <= 0x2B55) ||    // Stars, circles
               (v >= 0x25A0 && v <= 0x25FF) ||    // Geometric shapes
               (v >= 0x2702 && v <= 0x27B0) ||    // Dingbats
               (v >= 0x2934 && v <= 0x2935);      // Arrows
    }
}

public record PersistedTaskTemplate(
    Guid Id,
    string Title,
    string TaskListId,
    string TaskListName,
    bool DueDateToday,
    TimeOnly? ReminderTime,
    int SortOrder);

public static class DateRescheduleOptions
{
    /// <summary>
    /// Builds the list of quick-reschedule date options relative to <paramref name="today"/>.
    /// </summary>
    /// <param name="today">The current date in the user's timezone.</param>
    /// <param name="includeToday">When true, prepends a "Today" option.</param>
    public static List<(string Label, DateOnly Date)> Build(DateOnly today, bool includeToday)
    {
        var options = new List<(string Label, DateOnly Date)>();

        if (includeToday)
            options.Add(("Today", today));

        var tomorrow = today.AddDays(1);
        options.Add(("Tomorrow", tomorrow));

        // ISO week: Monday = start. Compute Sunday of the current week.
        var dotNetDow = (int)today.DayOfWeek; // Sun=0, Mon=1, ..., Sat=6
        var daysFromMonday = dotNetDow == 0 ? 6 : dotNetDow - 1;
        var thisWeekSunday = today.AddDays(6 - daysFromMonday);

        // Add remaining days of this week after tomorrow.
        var day = tomorrow.AddDays(1);
        while (day <= thisWeekSunday)
        {
            var dayName = day.DayOfWeek.ToString()[..3];
            options.Add(($"{dayName} {day.Day}", day));
            day = day.AddDays(1);
        }

        // Next week Monday = thisWeekMonday + 7 = today - daysFromMonday + 7.
        // Only show if it differs from tomorrow (avoids duplicate when today is Sunday).
        var nextMonday = today.AddDays(7 - daysFromMonday);
        if (nextMonday != tomorrow)
            options.Add(("Next week", nextMonday));

        return options;
    }
}
