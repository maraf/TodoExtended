using System.Text.RegularExpressions;

namespace TodoExtended.Web.Services;

public static partial class TagExtractor
{
    // Matches #word followed by a space (mid-title) or end-of-string (end of title).
    // Tag names: one or more word characters (letters, digits, underscore).
    [GeneratedRegex(@"#(\w+)(?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    /// <summary>
    /// Extracts distinct, lower-cased tag names from a task title.
    /// </summary>
    public static IReadOnlyList<string> ExtractTags(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return [];

        var matches = TagPattern().Matches(title);
        if (matches.Count == 0)
            return [];

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in matches)
            tags.Add(m.Groups[1].Value.ToLowerInvariant());

        return [.. tags];
    }

    /// <summary>
    /// Returns extracted tags as a space-separated string for storage, or null when there are none.
    /// </summary>
    public static string? ExtractTagsString(string title)
    {
        var tags = ExtractTags(title);
        return tags.Count == 0 ? null : string.Join(" ", tags);
    }

    /// <summary>
    /// Parses a stored space-separated tags string back into a list.
    /// </summary>
    public static IReadOnlyList<string> ParseTags(string? tagsString)
    {
        if (string.IsNullOrWhiteSpace(tagsString))
            return [];

        return tagsString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
