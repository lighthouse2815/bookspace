namespace BookSpace.Application.Abstractions;

public interface IChallengeProgressSynchronizer
{
    Task SaveChangesAndSyncAsync(Guid userId, CancellationToken cancellationToken);
    Task<TResult> SaveChangesAndSyncAsync<TResult>(
        Guid userId,
        Func<TResult> resultFactory,
        CancellationToken cancellationToken);
    Task SyncAsync(Guid userId, CancellationToken cancellationToken);
}
