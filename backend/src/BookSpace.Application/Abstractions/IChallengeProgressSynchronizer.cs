namespace BookSpace.Application.Abstractions;

public interface IChallengeProgressSynchronizer
{
    Task SaveChangesAndSyncAsync(Guid userId, CancellationToken cancellationToken);
    Task SyncAsync(Guid userId, CancellationToken cancellationToken);
}
