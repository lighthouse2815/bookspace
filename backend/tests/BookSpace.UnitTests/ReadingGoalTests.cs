using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class ReadingGoalTests
{
    [Fact]
    public void Goal_rejects_invalid_target()
    {
        var error = Assert.Throws<DomainException>(() =>
            new ReadingGoal(
                Guid.NewGuid(),
                ReadingGoalMetric.PAGES,
                ReadingGoalPeriod.MONTH,
                0,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1)));

        Assert.Equal("INVALID_READING_GOAL_TARGET", error.Code);
    }

    [Fact]
    public void Goal_rejects_duration_over_366_days()
    {
        var start = DateTimeOffset.UtcNow;
        var error = Assert.Throws<DomainException>(() =>
            new ReadingGoal(
                Guid.NewGuid(),
                ReadingGoalMetric.BOOKS,
                ReadingGoalPeriod.CUSTOM,
                12,
                start,
                start.AddDays(367)));

        Assert.Equal("INVALID_READING_GOAL_DATE", error.Code);
    }

    [Theory]
    [InlineData(999, 1, "INVALID_READING_GOAL_METRIC")]
    [InlineData(1, 999, "INVALID_READING_GOAL_PERIOD")]
    public void Goal_rejects_unknown_metric_or_period(
        int metric,
        int period,
        string expectedCode)
    {
        var error = Assert.Throws<DomainException>(() =>
            new ReadingGoal(
                Guid.NewGuid(),
                (ReadingGoalMetric)metric,
                (ReadingGoalPeriod)period,
                12,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1)));

        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public void Goal_completion_is_idempotent()
    {
        var goal = new ReadingGoal(
            Guid.NewGuid(),
            ReadingGoalMetric.MINUTES,
            ReadingGoalPeriod.WEEK,
            120,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(6));

        var completedAt = DateTimeOffset.UtcNow;
        Assert.True(goal.MarkCompleted(completedAt));
        Assert.False(goal.MarkCompleted(completedAt.AddMinutes(5)));
        Assert.Equal(completedAt, goal.CompletedAt);
        Assert.Equal(ReadingGoalStatus.COMPLETED, goal.StatusAt(completedAt));
    }

    [Fact]
    public async Task Service_rejects_overlapping_active_goal()
    {
        var repository = new FakeReadingGoalRepository { HasOverlap = true };
        var service = new ReadingGoalService(repository);

        var error = await Assert.ThrowsAsync<UseCaseException>(() =>
            service.CreateAsync(
                Guid.NewGuid(),
                new CreateReadingGoalRequest(
                    ReadingGoalMetric.PAGES,
                    ReadingGoalPeriod.MONTH,
                    500,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddMonths(1)),
                CancellationToken.None));

        Assert.Equal("READING_GOAL_OVERLAPS", error.Code);
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task Service_completes_goal_and_creates_one_notification()
    {
        var repository = new FakeReadingGoalRepository { ProgressOnAdd = 300 };
        var service = new ReadingGoalService(repository);
        var userId = Guid.NewGuid();

        var created = await service.CreateAsync(
            userId,
            new CreateReadingGoalRequest(
                ReadingGoalMetric.PAGES,
                ReadingGoalPeriod.WEEK,
                250,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(5)),
            CancellationToken.None);

        Assert.Equal(ReadingGoalStatus.COMPLETED, created.Status);
        Assert.Equal(100, created.ProgressPercent);
        Assert.NotNull(created.CompletedAt);
        Assert.Single(repository.Notifications);

        var loaded = await service.GetGoalAsync(userId, created.Id, CancellationToken.None);

        Assert.Equal(ReadingGoalStatus.COMPLETED, loaded.Status);
        Assert.Single(repository.Notifications);
    }

    private sealed class FakeReadingGoalRepository : IReadingGoalRepository
    {
        private readonly List<ReadingGoal> _goals = [];
        private readonly Dictionary<Guid, int> _progress = [];

        public bool HasOverlap { get; init; }
        public int ProgressOnAdd { get; init; }
        public List<Notification> Notifications { get; } = [];

        public Task<ReadingGoalSearchResult> SearchAsync(
            ReadingGoalSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            IEnumerable<ReadingGoal> query = _goals.Where(x =>
                x.UserId == criteria.UserId && !x.IsDeleted);
            query = criteria.Status switch
            {
                ReadingGoalStatus.ACTIVE => query.Where(x =>
                    x.CompletedAt == null && x.EndDate >= criteria.Now),
                ReadingGoalStatus.COMPLETED => query.Where(x => x.CompletedAt != null),
                ReadingGoalStatus.EXPIRED => query.Where(x =>
                    x.CompletedAt == null && x.EndDate < criteria.Now),
                _ => query
            };
            var total = query.LongCount();
            var items = query.Skip(criteria.Skip).Take(criteria.Take).ToList();
            return Task.FromResult(new ReadingGoalSearchResult(items, total));
        }

        public Task<ReadingGoal?> GetOwnedAsync(
            Guid userId,
            Guid goalId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_goals.FirstOrDefault(x =>
                x.Id == goalId && x.UserId == userId && !x.IsDeleted));

        public Task<IReadOnlyList<ReadingGoal>> GetPendingOwnedAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReadingGoal>>(
                _goals
                    .Where(x =>
                        x.UserId == userId &&
                        !x.IsDeleted &&
                        x.CompletedAt == null)
                    .ToList());

        public Task<bool> HasOverlappingActiveGoalAsync(
            Guid userId,
            ReadingGoalMetric metric,
            DateTimeOffset startDate,
            DateTimeOffset endDate,
            Guid? excludedGoalId,
            CancellationToken cancellationToken) =>
            Task.FromResult(HasOverlap);

        public Task<int> GetCurrentValueAsync(
            ReadingGoal goal,
            CancellationToken cancellationToken) =>
            Task.FromResult(_progress.GetValueOrDefault(goal.Id));

        public void Add(ReadingGoal goal)
        {
            _goals.Add(goal);
            _progress[goal.Id] = ProgressOnAdd;
        }

        public void AddNotification(Notification notification) =>
            Notifications.Add(notification);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
