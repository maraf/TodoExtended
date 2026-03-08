namespace TodoExtended.Web.Services;

public class NotificationService : INotificationService
{
    private readonly List<NotifyItem> _items = [];

    public event Action? Changed;
    public IReadOnlyList<NotifyItem> Items => _items;

    public void Add(string message, NotifySeverity severity = NotifySeverity.Info)
    {
        var item = new NotifyItem(Guid.NewGuid(), message, severity, DateTimeOffset.UtcNow.AddSeconds(5));
        _items.Add(item);
        Changed?.Invoke();
    }

    public void Dismiss(Guid id)
    {
        _items.RemoveAll(i => i.Id == id);
        Changed?.Invoke();
    }

    public void PurgeExpired()
    {
        var before = _items.Count;
        _items.RemoveAll(i => i.ExpiresAt < DateTimeOffset.UtcNow);
        if (_items.Count != before) Changed?.Invoke();
    }
}
