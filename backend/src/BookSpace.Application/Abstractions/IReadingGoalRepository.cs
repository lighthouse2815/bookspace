using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Abstractions;

public sealed record ReadingGoalSearchCriteria(
    Guid UserId,
    ReadingGoalStatus? Status,
    int Skip,
    int Take,
    DateTimeOffset Now);

public sealed record ReadingGoalSearchResult(
    IReadOnlyList<ReadingGoal> Items,
    long TotalItems);

public interface IReadingGoalRepository
{
    Task<ReadingGoalSearchResult> SearchAsync(
        ReadingGoalSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<ReadingGoal?> GetOwnedAsync(
        Guid userId,
        Guid goalId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReadingGoal>> GetPendingOwnedAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> HasOverlappingActiveGoalAsync(
        Guid userId,
        ReadingGoalMetric metric,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        Guid? excludedGoalId,
        CancellationToken cancellationToken);

    Task<int> GetCurrentValueAsync(ReadingGoal goal, CancellationToken cancellationToken);

    void Add(ReadingGoal goal);
    void AddNotification(Notification notification);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
