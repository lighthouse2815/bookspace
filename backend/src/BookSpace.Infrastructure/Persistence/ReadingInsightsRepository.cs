using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public sealed class ReadingInsightsRepository(BookSpaceDbContext db) : IReadingInsightsRepository
{
    public async Task<IReadOnlyList<ReadingSessionInsightData>> GetSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.ReadingSessionSet
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new ReadingSessionInsightData(
                x.BookId,
                x.StartedAt,
                x.PagesRead,
                x.DurationMinutes))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FinishedBookInsightData>> GetFinishedBooksAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var items = await db.LibraryItemSet
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Status == LibraryStatus.READ &&
                x.FinishedAt != null)
            .Select(x => new { x.BookId, x.FinishedAt })
            .ToListAsync(cancellationToken);
        return items
            .Select(x => new FinishedBookInsightData(x.BookId, x.FinishedAt!.Value))
            .ToList();
    }

    public async Task<ReadingGoalCountData> GetGoalCountsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var goals = await db.Set<ReadingGoal>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.CompletedAt, x.EndDate })
            .ToListAsync(cancellationToken);
        var completed = goals.Count(x => x.CompletedAt.HasValue);
        var expired = goals.Count(x => !x.CompletedAt.HasValue && x.EndDate < now);
        var active = goals.Count - completed - expired;
        return new ReadingGoalCountData(goals.Count, active, completed, expired);
    }

    public async Task<IReadOnlyList<ReadingBookInsightData>> GetReadingBooksAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.LibraryItemSet
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == LibraryStatus.READING)
            .Select(x => new ReadingBookInsightData(
                x.Id,
                x.BookId,
                x.Book.Title,
                x.Book.CoverUrl,
                x.CurrentPage,
                x.Book.PageCount))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ActiveReadingGoalInsightData>> GetActiveGoalsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var goals = await db.Set<ReadingGoal>()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.CompletedAt == null)
            .ToListAsync(cancellationToken);
        goals = goals
            .Where(x => x.EndDate >= now)
            .ToList();
        if (goals.Count == 0)
        {
            return [];
        }

        var sessions = await db.ReadingSessionSet
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.StartedAt,
                x.PagesRead,
                x.DurationMinutes
            })
            .ToListAsync(cancellationToken);
        var finishedBooks = await db.LibraryItemSet
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Status == LibraryStatus.READ &&
                x.FinishedAt != null)
            .Select(x => x.FinishedAt)
            .ToListAsync(cancellationToken);

        return goals
            .Select(goal =>
            {
                long value = goal.Metric switch
                {
                    ReadingGoalMetric.BOOKS => finishedBooks.LongCount(finishedAt =>
                        finishedAt >= goal.StartDate &&
                        finishedAt <= goal.EndDate),
                    ReadingGoalMetric.PAGES => sessions
                        .Where(x =>
                            x.StartedAt >= goal.StartDate &&
                            x.StartedAt <= goal.EndDate)
                        .Sum(x => (long)x.PagesRead),
                    ReadingGoalMetric.MINUTES => sessions
                        .Where(x =>
                            x.StartedAt >= goal.StartDate &&
                            x.StartedAt <= goal.EndDate)
                        .Sum(x => (long)x.DurationMinutes),
                    _ => 0
                };
                return new ActiveReadingGoalInsightData(
                    goal.Id,
                    goal.Metric,
                    goal.TargetValue,
                    (int)Math.Clamp(value, 0, int.MaxValue),
                    goal.StartDate,
                    goal.EndDate);
            })
            .ToList();
    }
}
