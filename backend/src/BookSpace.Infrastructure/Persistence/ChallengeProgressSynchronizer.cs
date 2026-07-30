using BookSpace.Application.Abstractions;
using BookSpace.Application.Services;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public sealed class ChallengeProgressSynchronizer(BookSpaceDbContext db)
    : IChallengeProgressSynchronizer
{
    public async Task SaveChangesAndSyncAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await SyncCoreAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SyncAsync(Guid userId, CancellationToken cancellationToken)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var transaction =
                    await db.Database.BeginTransactionAsync(cancellationToken);
                await SyncCoreAsync(userId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (
                IsSqliteBusy(exception) &&
                attempt < maxAttempts)
            {
                foreach (var entry in db.ChangeTracker
                    .Entries<Notification>()
                    .Where(x =>
                        x.State == EntityState.Added &&
                        x.Entity.DeduplicationKey != null))
                {
                    entry.State = EntityState.Detached;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    private async Task SyncCoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        var participations = await db.ChallengeParticipationSet
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        if (participations.Count == 0)
        {
            return;
        }

        var challengeIds = participations.Select(x => x.ChallengeId).ToList();
        var challenges = await db.ReadingChallengeSet
            .AsNoTracking()
            .Where(x => challengeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var finishedBooks = await db.LibraryItemSet
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Status == LibraryStatus.READ &&
                x.FinishedAt != null)
            .Select(x => x.FinishedAt!.Value)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var participation in participations)
        {
            if (!challenges.TryGetValue(participation.ChallengeId, out var challenge))
            {
                continue;
            }

            var next = ChallengeProgress.Derive(
                finishedBooks,
                challenge.StartsAt,
                challenge.EndsAt,
                challenge.TargetBooks,
                participation.CompletedBooks);
            if (next > participation.CompletedBooks ||
                (next >= challenge.TargetBooks && !participation.CompletedAt.HasValue))
            {
                await db.ChallengeParticipationSet
                    .Where(x =>
                        x.Id == participation.Id &&
                        (x.CompletedBooks < next ||
                         (x.CompletedBooks >= challenge.TargetBooks &&
                          x.CompletedAt == null)))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                x => x.CompletedBooks,
                                x => x.CompletedBooks < next
                                    ? next
                                    : x.CompletedBooks)
                            .SetProperty(
                                x => x.CompletedAt,
                                x => next >= challenge.TargetBooks && x.CompletedAt == null
                                    ? now
                                    : x.CompletedAt)
                            .SetProperty(x => x.UpdatedAt, now),
                        cancellationToken);
            }
        }

        await CreateCompletionNotificationsAsync(
            userId,
            challenges,
            cancellationToken);
        await ReloadTrackedParticipationsAsync(userId, cancellationToken);
    }

    private async Task CreateCompletionNotificationsAsync(
        Guid userId,
        IReadOnlyDictionary<Guid, ReadingChallenge> challenges,
        CancellationToken cancellationToken)
    {
        var challengeIds = challenges.Keys.ToList();
        var completedChallengeIds = await db.ChallengeParticipationSet
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.CompletedAt != null &&
                challengeIds.Contains(x.ChallengeId))
            .Select(x => x.ChallengeId)
            .ToListAsync(cancellationToken);
        foreach (var challengeId in completedChallengeIds)
        {
            var deduplicationKey = CompletionEventKey(userId, challengeId);
            if (await db.NotificationSet
                .AsNoTracking()
                .AnyAsync(
                    x => x.DeduplicationKey == deduplicationKey,
                    cancellationToken))
            {
                continue;
            }

            var challenge = challenges[challengeId];
            var notification = new Notification(
                userId,
                NotificationType.CHALLENGE,
                "Hoàn thành thử thách",
                $"Chúc mừng! Bạn đã hoàn thành “{challenge.Title}”.",
                $"/challenges/{challenge.Id}",
                deduplicationKey);
            db.NotificationSet.Add(notification);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is SqliteException
                {
                    SqliteErrorCode: 19,
                    SqliteExtendedErrorCode: 2067
                })
            {
                db.Entry(notification).State = EntityState.Detached;
            }
        }
    }

    private async Task ReloadTrackedParticipationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tracked = db.ChangeTracker
            .Entries<ChallengeParticipation>()
            .Where(x => x.Entity.UserId == userId)
            .ToList();
        foreach (var entry in tracked)
        {
            await entry.ReloadAsync(cancellationToken);
        }
    }

    public static string CompletionEventKey(Guid userId, Guid challengeId) =>
        $"challenge-completed:{challengeId:N}:{userId:N}";

    private static bool IsSqliteBusy(Exception exception)
    {
        var sqliteException = exception switch
        {
            SqliteException direct => direct,
            DbUpdateException { InnerException: SqliteException inner } => inner,
            _ => null
        };
        return sqliteException?.SqliteErrorCode is 5 or 6;
    }
}
