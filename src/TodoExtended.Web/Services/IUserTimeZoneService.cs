namespace TodoExtended.Web.Services;

public interface IUserTimeZoneService
{
    /// <summary>
    /// Gets the current user's <see cref="TimeZoneInfo"/> from the DB (populated
    /// from Graph mailboxSettings on first login). Falls back to UTC.
    /// </summary>
    Task<TimeZoneInfo> GetCurrentUserTimeZoneAsync();

    /// <summary>
    /// Gets "today" in the current user's timezone.
    /// </summary>
    async Task<DateOnly> GetTodayAsync()
    {
        var tz = await GetCurrentUserTimeZoneAsync();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }
}
