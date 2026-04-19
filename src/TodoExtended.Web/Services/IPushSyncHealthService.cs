namespace TodoExtended.Web.Services;

public interface IPushSyncHealthService
{
    Task<bool> IsHealthyAsync(string userId, CancellationToken cancellationToken = default);

    Task RecordSuccessAsync(string userId, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(string userId, CancellationToken cancellationToken = default);
}

public class NoOpPushSyncHealthService : IPushSyncHealthService
{
    public Task<bool> IsHealthyAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task RecordSuccessAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RecordFailureAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
