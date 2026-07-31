using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public sealed class ChallengeProgressPersistence(BookSpaceDbContext db)
    : IChallengeProgressPersistence
{
    public async Task ExecuteRetryableSyncTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var transaction =
                    await db.Database.BeginTransactionAsync(cancellationToken);
                await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (
                IsSqliteBusy(exception) &&
                attempt < maxAttempts)
            {
                DetachRolledBackCompletionNotifications();
                await Task.Delay(
                    TimeSpan.FromMilliseconds(25 * attempt),
                    cancellationToken);
            }
        }
    }

    public async Task<ChallengeProgressWriteResult?> AdvanceHighWaterAsync(
        Guid participationId,
        int candidateBooks,
        int targetBooks,
        DateTimeOffset completedAtIfReached,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        if (targetBooks < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetBooks),
                "Mục tiêu thử thách phải lớn hơn 0.");
        }

        var boundedCandidate = Math.Clamp(candidateBooks, 0, targetBooks);
        await db.ChallengeParticipationSet
            .IgnoreQueryFilters()
            .Where(x =>
                x.Id == participationId &&
                (x.CompletedBooks < boundedCandidate ||
                 x.CompletedBooks > targetBooks ||
                 (x.CompletedAt == null &&
                  (x.CompletedBooks >= targetBooks ||
                   boundedCandidate >= targetBooks))))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.CompletedBooks,
                        x => x.CompletedBooks >= targetBooks
                            ? targetBooks
                            : x.CompletedBooks < boundedCandidate
                                ? boundedCandidate
                                : x.CompletedBooks)
                    .SetProperty(
                        x => x.CompletedAt,
                        x => x.CompletedAt == null &&
                             (x.CompletedBooks >= targetBooks ||
                              boundedCandidate >= targetBooks)
                            ? completedAtIfReached
                            : x.CompletedAt)
                    .SetProperty(x => x.UpdatedAt, updatedAt),
                cancellationToken);

        var persisted = await db.ChallengeParticipationSet
            .IgnoreQueryFilters()
            .Where(x => x.Id == participationId)
            .Select(x => new ChallengeProgressWriteResult(
                x.CompletedBooks,
                x.CompletedAt))
            .SingleOrDefaultAsync(cancellationToken);

        var tracked = db.ChangeTracker
            .Entries<ChallengeParticipation>()
            .FirstOrDefault(x => x.Entity.Id == participationId);
        if (persisted is not null && tracked is not null)
        {
            await tracked.ReloadAsync(cancellationToken);
        }

        return persisted;
    }

    public async Task<bool> TryAddNotificationAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        var deduplicationKey = notification.DeduplicationKey
            ?? throw new InvalidOperationException(
                "Thông báo đồng bộ thử thách phải có khóa chống trùng.");
        if (await db.NotificationSet
            .AnyAsync(
                x => x.DeduplicationKey == deduplicationKey,
                cancellationToken))
        {
            return false;
        }

        db.NotificationSet.Add(notification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            IsDeduplicationKeyUniqueViolation(exception))
        {
            db.Entry(notification).State = EntityState.Detached;
            return false;
        }
    }

    private void DetachRolledBackCompletionNotifications()
    {
        foreach (var entry in db.ChangeTracker
            .Entries<Notification>()
            .Where(x =>
                x.Entity.DeduplicationKey != null &&
                x.State is EntityState.Added or EntityState.Unchanged))
        {
            entry.State = EntityState.Detached;
        }
    }

    private static bool IsDeduplicationKeyUniqueViolation(
        DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        } sqliteException &&
        sqliteException.Message.Contains(
            "notifications.DeduplicationKey",
            StringComparison.OrdinalIgnoreCase);

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
