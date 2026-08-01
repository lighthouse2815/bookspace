using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ChallengeProgressSynchronizer(
    IBookSpaceDbContext db,
    IAsyncQueryExecutor queryExecutor,
    IChallengeMutationBoundary mutationBoundary,
    IChallengeProgressPersistence persistence,
    TimeProvider timeProvider) : IChallengeProgressSynchronizer
{
    public async Task SaveChangesAndSyncAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await mutationBoundary.ExecuteAsync(
            async transactionCancellationToken =>
            {
                await db.SaveChangesAsync(transactionCancellationToken);
                await SyncCoreAsync(userId, transactionCancellationToken);
                return true;
            },
            cancellationToken);
    }

    public Task<TResult> ExecuteMutationAndSyncAsync<TResult>(
        Guid userId,
        Func<CancellationToken, Task> mutation,
        Func<TResult> resultFactory,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async transactionCancellationToken =>
            {
                await mutation(transactionCancellationToken);
                await db.SaveChangesAsync(transactionCancellationToken);
                await SyncCoreAsync(userId, transactionCancellationToken);
                return resultFactory();
            },
            cancellationToken);

    public Task SyncAsync(Guid userId, CancellationToken cancellationToken) =>
        persistence.ExecuteRetryableSyncTransactionAsync(
            transactionCancellationToken =>
                SyncCoreAsync(userId, transactionCancellationToken),
            cancellationToken);

    private async Task SyncCoreAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var participations = await queryExecutor.ToListAsync(
            db.ChallengeParticipations
                .Where(x => x.UserId == userId)
                .Select(x => new ParticipationSnapshot(
                    x.Id,
                    x.ChallengeId,
                    x.CompletedBooks)),
            cancellationToken);
        if (participations.Count == 0)
        {
            return;
        }

        var challengeIds = participations.Select(x => x.ChallengeId).ToList();
        var challengeSnapshots = await queryExecutor.ToListAsync(
            db.ReadingChallenges
                .Where(x => challengeIds.Contains(x.Id))
                .Select(x => new ChallengeSnapshot(
                    x.Id,
                    x.Title,
                    x.StartsAt,
                    x.EndsAt,
                    x.TargetBooks)),
            cancellationToken);
        var challenges = challengeSnapshots.ToDictionary(x => x.Id);
        var finishedBooks = await queryExecutor.ToListAsync(
            db.LibraryItems
                .Where(x =>
                    x.UserId == userId &&
                    x.Status == LibraryStatus.READ &&
                    x.FinishedAt != null)
                .Select(x => x.FinishedAt!.Value),
            cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var participation in participations)
        {
            if (!challenges.TryGetValue(participation.ChallengeId, out var challenge))
            {
                continue;
            }

            var candidate = ChallengeProgress.Derive(
                finishedBooks,
                challenge.StartsAt,
                challenge.EndsAt,
                challenge.TargetBooks,
                participation.CompletedBooks);
            var persisted = await persistence.AdvanceHighWaterAsync(
                participation.Id,
                candidate,
                challenge.TargetBooks,
                now,
                now,
                cancellationToken);
            if (persisted?.CompletedAt is null)
            {
                continue;
            }

            if (!NotificationDelivery.IsEnabled(db, userId, NotificationType.CHALLENGE))
            {
                continue;
            }

            var notification = new Notification(
                userId,
                NotificationType.CHALLENGE,
                "Hoàn thành thử thách",
                $"Chúc mừng! Bạn đã hoàn thành “{challenge.Title}”.",
                $"/challenges/{challenge.Id}",
                CompletionEventKey(userId, challenge.Id));
            await persistence.TryAddNotificationAsync(
                notification,
                cancellationToken);
        }
    }

    public static string CompletionEventKey(Guid userId, Guid challengeId) =>
        $"challenge-completed:{challengeId:N}:{userId:N}";

    private sealed record ParticipationSnapshot(
        Guid Id,
        Guid ChallengeId,
        int CompletedBooks);

    private sealed record ChallengeSnapshot(
        Guid Id,
        string Title,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        int TargetBooks);
}
