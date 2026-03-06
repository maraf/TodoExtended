namespace TodoExtended.Web.Services;

public interface IUserPreferenceService
{
    Task<bool> GetIsDarkModeAsync(string userId);
    Task SetIsDarkModeAsync(string userId, bool isDarkMode);
}
