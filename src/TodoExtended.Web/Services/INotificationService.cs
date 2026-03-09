namespace TodoExtended.Web.Services;

public enum NotifySeverity { Success, Error, Warning, Info }

public record NotifyItem(Guid Id, string Message, NotifySeverity Severity, DateTimeOffset ExpiresAt);

public interface INotificationService
{
    event Action? Changed;
    IReadOnlyList<NotifyItem> Items { get; }
    void Add(string message, NotifySeverity severity = NotifySeverity.Info);
    void Dismiss(Guid id);
    void PurgeExpired();
}
