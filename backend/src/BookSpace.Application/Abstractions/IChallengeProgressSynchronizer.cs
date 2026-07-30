namespace BookSpace.Application.Abstractions;

public interface IChallengeProgressSynchronizer
{
    Task SaveChangesAndSyncAsync(Guid userId, CancellationToken cancellationToken);
    Task<TResult> ExecuteMutationAndSyncAsync<TResult>(
        Guid userId,
        Func<CancellationToken, Task> mutation,
        Func<TResult> resultFactory,
        CancellationToken cancellationToken);
    Task SyncAsync(Guid userId, CancellationToken cancellationToken);
}
