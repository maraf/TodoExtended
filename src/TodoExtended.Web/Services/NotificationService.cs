namespace TodoExtended.Web.Services;

public class NotificationService : INotificationService
{
    private const int ExpirySeconds = 3;

    private readonly List<NotifyItem> _items = [];
    private readonly object _lock = new();

    public event Action? Changed;

    public IReadOnlyList<NotifyItem> Items
    {
        get { lock (_lock) { return _items.ToList(); } }
    }

    public void Add(string message, NotifySeverity severity = NotifySeverity.Info)
    {
        var item = new NotifyItem(Guid.NewGuid(), message, severity, DateTimeOffset.UtcNow.AddSeconds(ExpirySeconds));
        lock (_lock) { _items.Add(item); }
        Changed?.Invoke();
    }

    public void Dismiss(Guid id)
    {
        lock (_lock) { _items.RemoveAll(i => i.Id == id); }
        Changed?.Invoke();
    }

    public void PurgeExpired()
    {
        int before;
        lock (_lock)
        {
            before = _items.Count;
            _items.RemoveAll(i => i.ExpiresAt < DateTimeOffset.UtcNow);
            if (_items.Count == before) return;
        }
        Changed?.Invoke();
    }
}
