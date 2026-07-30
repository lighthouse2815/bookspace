using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public sealed class ReadingGoalRepository(BookSpaceDbContext db) : IReadingGoalRepository
{
    private DbSet<ReadingGoal> Goals => db.Set<ReadingGoal>();

    public async Task<ReadingGoalSearchResult> SearchAsync(
        ReadingGoalSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = Goals.Where(x => x.UserId == criteria.UserId);
        query = criteria.Status switch
        {
            ReadingGoalStatus.ACTIVE => query.Where(x =>
                x.CompletedAt == null && x.EndDate >= criteria.Now),
            ReadingGoalStatus.COMPLETED => query.Where(x => x.CompletedAt != null),
            ReadingGoalStatus.EXPIRED => query.Where(x =>
                x.CompletedAt == null && x.EndDate < criteria.Now),
            _ => query
        };

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CompletedAt == null && x.EndDate < criteria.Now)
            .ThenBy(x => x.EndDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip(criteria.Skip)
            .Take(criteria.Take)
            .ToListAsync(cancellationToken);
        return new ReadingGoalSearchResult(items, total);
    }

    public Task<ReadingGoal?> GetOwnedAsync(
        Guid userId,
        Guid goalId,
        CancellationToken cancellationToken) =>
        Goals.FirstOrDefaultAsync(
            x => x.Id == goalId && x.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyList<ReadingGoal>> GetPendingOwnedAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await Goals
            .Where(x => x.UserId == userId && x.CompletedAt == null)
            .ToListAsync(cancellationToken);

    public Task<bool> HasOverlappingActiveGoalAsync(
        Guid userId,
        ReadingGoalMetric metric,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        Guid? excludedGoalId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return Goals.AnyAsync(
            x =>
                x.UserId == userId &&
                x.Metric == metric &&
                x.CompletedAt == null &&
                x.EndDate >= now &&
                (!excludedGoalId.HasValue || x.Id != excludedGoalId.Value) &&
                x.StartDate < endDate &&
                startDate < x.EndDate,
            cancellationToken);
    }

    public async Task<int> GetCurrentValueAsync(
        ReadingGoal goal,
        CancellationToken cancellationToken)
    {
        long value = goal.Metric switch
        {
            ReadingGoalMetric.BOOKS => await db.LibraryItemSet
                .AsNoTracking()
                .LongCountAsync(
                    x =>
                        x.UserId == goal.UserId &&
                        x.Status == LibraryStatus.READ &&
                        x.FinishedAt != null &&
                        x.FinishedAt >= goal.StartDate &&
                        x.FinishedAt <= goal.EndDate,
                    cancellationToken),
            ReadingGoalMetric.PAGES => await db.ReadingSessionSet
                .AsNoTracking()
                .Where(x =>
                    x.UserId == goal.UserId &&
                    x.StartedAt >= goal.StartDate &&
                    x.StartedAt <= goal.EndDate)
                .Select(x => (long?)x.PagesRead)
                .SumAsync(cancellationToken) ?? 0,
            ReadingGoalMetric.MINUTES => await db.ReadingSessionSet
                .AsNoTracking()
                .Where(x =>
                    x.UserId == goal.UserId &&
                    x.StartedAt >= goal.StartDate &&
                    x.StartedAt <= goal.EndDate)
                .Select(x => (long?)x.DurationMinutes)
                .SumAsync(cancellationToken) ?? 0,
            _ => 0
        };

        return (int)Math.Clamp(value, 0, int.MaxValue);
    }

    public void Add(ReadingGoal goal) => Goals.Add(goal);

    public void AddNotification(Notification notification) =>
        db.NotificationSet.Add(notification);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
