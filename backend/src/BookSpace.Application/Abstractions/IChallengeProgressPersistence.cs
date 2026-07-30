using BookSpace.Domain.Entities;

namespace BookSpace.Application.Abstractions;

public sealed record ChallengeProgressWriteResult(
    int CompletedBooks,
    DateTimeOffset? CompletedAt);

public sealed class DuplicateChallengeParticipationException(
    Exception innerException)
    : Exception("Challenge participation already exists.", innerException);

public interface IChallengeProgressPersistence
{
    Task ExecuteMutationTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken);

    Task ExecuteRetryableSyncTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken);

    Task<ChallengeProgressWriteResult?> AdvanceHighWaterAsync(
        Guid participationId,
        int candidateBooks,
        int targetBooks,
        DateTimeOffset completedAtIfReached,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task<bool> TryAddNotificationAsync(
        Notification notification,
        CancellationToken cancellationToken);
}
