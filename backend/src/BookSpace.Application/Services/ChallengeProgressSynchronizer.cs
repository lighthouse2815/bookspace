using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ChallengeProgressSynchronizer(
    IBookSpaceDbContext db,
    IChallengeProgressPersistence persistence,
    TimeProvider timeProvider) : IChallengeProgressSynchronizer
{
    public Task SaveChangesAndSyncAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        persistence.ExecuteMutationTransactionAsync(
            async transactionCancellationToken =>
            {
                await db.SaveChangesAsync(transactionCancellationToken);
                await SyncCoreAsync(userId, transactionCancellationToken);
            },
            cancellationToken);

    public async Task<TResult> SaveChangesAndSyncAsync<TResult>(
        Guid userId,
        Func<TResult> resultFactory,
        CancellationToken cancellationToken)
    {
        TResult? result = default;
        await persistence.ExecuteMutationTransactionAsync(
            async transactionCancellationToken =>
            {
                await db.SaveChangesAsync(transactionCancellationToken);
                await SyncCoreAsync(userId, transactionCancellationToken);
                result = resultFactory();
            },
            cancellationToken);
        return result!;
    }

    public Task SyncAsync(Guid userId, CancellationToken cancellationToken) =>
        persistence.ExecuteRetryableSyncTransactionAsync(
            transactionCancellationToken =>
                SyncCoreAsync(userId, transactionCancellationToken),
            cancellationToken);

    private async Task SyncCoreAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var participations = db.ChallengeParticipations
            .Where(x => x.UserId == userId)
            .Select(x => new ParticipationSnapshot(
                x.Id,
                x.ChallengeId,
                x.CompletedBooks))
            .ToList();
        if (participations.Count == 0)
        {
            return;
        }

        var challengeIds = participations.Select(x => x.ChallengeId).ToList();
        var challenges = db.ReadingChallenges
            .Where(x => challengeIds.Contains(x.Id))
            .Select(x => new ChallengeSnapshot(
                x.Id,
                x.Title,
                x.StartsAt,
                x.EndsAt,
                x.TargetBooks))
            .ToDictionary(x => x.Id);
        var finishedBooks = db.LibraryItems
            .Where(x =>
                x.UserId == userId &&
                x.Status == LibraryStatus.READ &&
                x.FinishedAt != null)
            .Select(x => x.FinishedAt!.Value)
            .ToList();
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
