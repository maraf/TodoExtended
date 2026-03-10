using System.Globalization;
using System.Text;

namespace TodoExtended.Web.Services;

public record PersistedTodoTask(
    string Id,
    string Title,
    bool IsCompleted,
    DateOnly? DueDate,
    string? Importance);

public record PersistedTodoTaskWithList(
    string Id,
    string Title,
    bool IsCompleted,
    string? Importance,
    string ListId,
    string ListName);

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
