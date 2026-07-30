using BookSpace.Domain.Enums;

namespace BookSpace.Application.Abstractions;

public sealed record ReadingSessionInsightData(
    Guid BookId,
    DateTimeOffset StartedAt,
    int PagesRead,
    int DurationMinutes);

public sealed record FinishedBookInsightData(
    Guid BookId,
    DateTimeOffset FinishedAt);

public sealed record ReadingGoalCountData(
    int Total,
    int Active,
    int Completed,
    int Expired);

public sealed record ReadingBookInsightData(
    Guid LibraryItemId,
    Guid BookId,
    string Title,
    string? CoverImageUrl,
    int CurrentPage,
    int PageCount);

public sealed record ActiveReadingGoalInsightData(
    Guid GoalId,
    ReadingGoalMetric Metric,
    int TargetValue,
    int CurrentValue,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate);

public interface IReadingInsightsRepository
{
    Task<IReadOnlyList<ReadingSessionInsightData>> GetSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FinishedBookInsightData>> GetFinishedBooksAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<ReadingGoalCountData> GetGoalCountsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReadingBookInsightData>> GetReadingBooksAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ActiveReadingGoalInsightData>> GetActiveGoalsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
